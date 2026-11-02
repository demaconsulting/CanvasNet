// Portions derived from Vega-Lite (BSD-3-Clause): compile/scale/domain.ts (mergeDomains), compile/scale/parse.ts and compile/resolve.ts; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     One layer of a layered view as the scale resolution sees it.
/// </summary>
/// <param name="Node">The unit view in the layer tree (its <see cref="LayerNode.Unit"/> is the view the scales are resolved for, and the resolves of the layered views above it decide what it shares).</param>
/// <param name="Plan">The plan of the layer's encoding.</param>
/// <param name="Data">The tables of the layer's implicit pipeline.</param>
internal sealed record LayerScaleInput(LayerNode Node, EncodingPlan Plan, EncodingData Data);

/// <summary>
///     The scales of the layers of a layered view.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="PerLayer"/> holds the scales each layer's marks use; where layers share a channel's scale they all hold a scale with the same kind, domain, padding, step, ranges and
///         transform, bound to the layer's own field. <see cref="Merged"/> holds one scale for each channel that the whole view resolves to a single scale (a channel of one layer only, or one
///         that all the layers that carry it share); it is what the axes, the legends and the size of the view read. A channel that resolves to several scales (an independent scale, or layers
///         whose scales cannot be merged) has no entry in <see cref="Merged"/>; <see cref="Groups"/> lists which layers share a scale.
///     </para>
///     <para>The object is immutable.</para>
/// </remarks>
internal sealed class LayeredScales
{
    /// <summary>
    ///     Initializes the result.
    /// </summary>
    /// <param name="perLayer">The scales of each layer.</param>
    /// <param name="merged">The scales of the whole view.</param>
    /// <param name="groups">The layers that share a scale, by channel.</param>
    /// <param name="warnings">The warnings found while merging.</param>
    internal LayeredScales(
        IReadOnlyList<ResolvedScales> perLayer,
        ResolvedScales merged,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<int>>> groups,
        IReadOnlyList<FeatureReport> warnings)
    {
        PerLayer = perLayer;
        Merged = merged;
        Groups = groups;
        Warnings = warnings;
    }

    /// <summary>Gets the scales of each layer, in layer order.</summary>
    public IReadOnlyList<ResolvedScales> PerLayer { get; }

    /// <summary>Gets the scales of the whole view; a single-layer view has the very object of <see cref="PerLayer"/>.</summary>
    public ResolvedScales Merged { get; }

    /// <summary>
    ///     Gets, for each channel that some layer carries, the groups of layers (indexes into <see cref="PerLayer"/>) that share one scale. A channel with one group has one scale
    ///     for the whole view; a layer that no other layer shares a scale with is a group of its own.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<int>>> Groups { get; }

    /// <summary>Gets the <c>vl-layer-scale-conflict</c> warnings: the layers whose scale could not be merged and kept their own.</summary>
    public IReadOnlyList<FeatureReport> Warnings { get; }
}

/// <content>
///     The scales of the layers of a layered view: shared scales are resolved once for the layers that share them, with the combined domain.
/// </content>
/// <remarks>
///     <para>
///         The rule is Vega-Lite's (<c>resolve.scale</c>, default shared). Every layer's scales are resolved on their own first. For each channel, the layers whose scales are shared (see
///         <see cref="LayerResolve.Scale"/> and <see cref="LayerNode"/>: a layer shares with the others below the highest layered view above it that does not make the channel independent)
///         are merged:
///     </para>
///     <list type="bullet">
///         <item><description>
///             The <b>owner</b> is the layer whose kind of scale has the highest precedence (band before point before the others; the first layer among equals). Layers whose scale cannot be
///             combined with the owner's (a continuous scale with a discrete one, a linear scale with a logarithmic or a time scale) keep their own scale and are reported as
///             <c>vl-layer-scale-conflict</c>.
///         </description></item>
///         <item><description>
///             The <b>domain</b> is combined: for continuous scales the smallest start and the largest end of the layers' domains (after each layer's zero rule); for discrete scales
///             the categories in layer order without repeats, sorted ascending or descending when every layer sorts that way. An explicit <c>scale.domain</c> of a layer, and the
///             domain of a diverging, quantile, quantize, threshold or bin-ordinal scale, is not combined: the first layer that has one decides (Vega-Lite concatenates explicit domains,
///             a documented deviation).
///         </description></item>
///         <item><description>
///             The owner's scale is resolved <b>again</b> with the combined domain, so everything that depends on the domain (the size of an ordinal range, the extent of a step) is
///             consistent, and each layer gets that scale bound to its own field, title, bin and stack.
///         </description></item>
///     </list>
///     <para>
///         The cost is one more resolution of the channel for each merged group; the data is not read again except to resolve that one channel.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>The precedence of a scale kind when layers merge a scale: Vega-Lite's <c>SCALE_PRECEDENCE_INDEX</c> puts band before point before the rest.</summary>
    private static int Precedence(ScaleKind kind) => kind switch
    {
        ScaleKind.Band => 2,
        ScaleKind.Point => 1,
        _ => 0,
    };

    /// <summary>
    ///     Resolves the scales of the layers of a layered view.
    /// </summary>
    /// <remarks>
    ///     A single layer is resolved exactly as <see cref="Resolve(SpecNode, EncodingPlan, EncodingData, ResolvedConfig, string?, VegaLiteLimits?, CancellationToken)"/> does, and
    ///     <see cref="LayeredScales.Merged"/> is the same object as its scales.
    /// </remarks>
    /// <param name="layers">The unit views of the layered view in layer order, with their plans and tables.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="sourceName">The source name used in the locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">A token that stops the resolution.</param>
    /// <returns>The scales of each layer and of the whole view.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than the optional ones is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="layers"/> is empty or a layer has no unit view.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when <c>scale.domain</c> or <c>scale.range</c> has the wrong shape; carries the location.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a domain, including the combined one, exceeds <see cref="VegaLiteLimits.MaxScaleDomainSize"/>; carries the location.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static LayeredScales ResolveLayers(
        IReadOnlyList<LayerScaleInput> layers,
        ResolvedConfig config,
        string? sourceName = null,
        VegaLiteLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(config);
        if (layers.Count == 0 || layers.Any(l => l.Node.Unit is null))
        {
            throw new ArgumentException("At least one layer, each with a unit view, is required.", nameof(layers));
        }

        if (layers.Count == 1)
        {
            var only = layers[0];
            var single = Resolve(only.Node.Unit!, only.Plan, only.Data, config, sourceName, limits, cancellationToken);
            var alone = new Dictionary<string, IReadOnlyList<IReadOnlyList<int>>>(StringComparer.Ordinal);
            foreach (var scale in single.Scales)
            {
                alone[scale.Channel] = new List<IReadOnlyList<int>> { new[] { 0 } };
            }

            return new LayeredScales([single], single, alone, []);
        }

        // Every layer on its own first
        var contexts = new Context?[layers.Count];
        var maps = new Dictionary<string, ResolvedScale>[layers.Count];
        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            contexts[i] = CreateContext(layer.Node.Unit!, layer.Plan, layer.Data, config, sourceName, limits, cancellationToken);
            maps[i] = contexts[i] is { } context ? ResolveChannels(context) : new Dictionary<string, ResolvedScale>(StringComparer.Ordinal);
        }

        // Then the scales that layers share
        var warnings = new List<FeatureReport>();
        var groups = new Dictionary<string, IReadOnlyList<IReadOnlyList<int>>>(StringComparer.Ordinal);
        var merged = new Dictionary<string, ResolvedScale>(StringComparer.Ordinal);
        foreach (var channel in Channels)
        {
            var carriers = Enumerable.Range(0, layers.Count).Where(i => maps[i].ContainsKey(channel)).ToArray();
            if (carriers.Length == 0)
            {
                continue;
            }

            var channelGroups = new List<IReadOnlyList<int>>();
            var groupScales = new List<ResolvedScale>();
            foreach (var group in GroupLayers(layers, carriers, channel))
            {
                var merge = group.Count >= 2 ? MergeGroup(channel, group, contexts, maps, warnings, limits ?? VegaLiteLimits.Default) : null;
                if (merge is not null)
                {
                    channelGroups.Add(merge.Members);
                    groupScales.Add(merge.Scale);
                }

                // A layer that is alone, or whose scale could not be merged, has a scale of its own
                foreach (var i in merge is null ? group : group.Except(merge.Members))
                {
                    channelGroups.Add([i]);
                    groupScales.Add(maps[i][channel]);
                }
            }

            groups[channel] = channelGroups;
            if (channelGroups.Count == 1)
            {
                merged[channel] = groupScales[0];
            }
        }

        var perLayer = new ResolvedScales[layers.Count];
        for (var i = 0; i < layers.Count; i++)
        {
            perLayer[i] = new ResolvedScales(layers[i].Plan.MarkType, layers[i].Plan.Orient, maps[i], layers[i].Plan);
        }

        var first = layers[0];
        return new LayeredScales(perLayer, new ResolvedScales(first.Plan.MarkType, first.Plan.Orient, merged, first.Plan), groups, warnings);
    }

    /// <summary>
    ///     Divides the layers that carry a channel into the groups that share its scale.
    /// </summary>
    /// <remarks>
    ///     A layer shares with the other layers below the highest layered view above it, going up from the layer, such that every layered view on the way (above the layer) resolves the
    ///     channel's scale as shared. A layered view that makes the channel independent keeps the scales of the layers below it apart.
    /// </remarks>
    /// <param name="layers">The layers.</param>
    /// <param name="carriers">The indexes of the layers that carry the channel.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The groups of layer indexes, in order of their first layer.</returns>
    private static List<List<int>> GroupLayers(IReadOnlyList<LayerScaleInput> layers, int[] carriers, string channel)
    {
        var byRoot = new Dictionary<LayerNode, List<int>>();
        var order = new List<List<int>>();
        foreach (var index in carriers)
        {
            var top = layers[index].Node;
            while (top.Parent is { } parent && parent.Resolve.Scale(channel) == ResolveMode.Shared)
            {
                top = parent;
            }

            if (!byRoot.TryGetValue(top, out var group))
            {
                group = [];
                byRoot[top] = group;
                order.Add(group);
            }

            group.Add(index);
        }

        return order;
    }

    /// <summary>The result of merging the scales of a group of layers.</summary>
    /// <param name="Scale">The scale for the whole group (the owner's, with the titles joined).</param>
    /// <param name="Members">The layers that now use the shared scale.</param>
    private sealed record MergeResult(ResolvedScale Scale, IReadOnlyList<int> Members);

    /// <summary>The domain that the layers of a group share.</summary>
    /// <param name="Channel">The channel.</param>
    /// <param name="Start">The start of a continuous domain.</param>
    /// <param name="End">The end of a continuous domain.</param>
    /// <param name="Entries">The entries of a piecewise or diverging domain, or null.</param>
    /// <param name="Categories">The categories of a discrete domain, or null for a continuous one.</param>
    private sealed record SharedDomain(string Channel, double Start, double End, double[]? Entries, IReadOnlyList<DataValue>? Categories);

    /// <summary>
    ///     Merges the scales of a group of layers that share a channel's scale, and stores the shared scale (bound to each layer's field) in their scale maps.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="group">The layers of the group.</param>
    /// <param name="contexts">The facts of each layer.</param>
    /// <param name="maps">The scales of each layer; the members' entries are replaced.</param>
    /// <param name="warnings">Receives the conflicts.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The merge; null when fewer than two layers could be merged.</returns>
    private static MergeResult? MergeGroup(
        string channel,
        List<int> group,
        Context?[] contexts,
        Dictionary<string, ResolvedScale>[] maps,
        List<FeatureReport> warnings,
        VegaLiteLimits limits)
    {
        // The owner has the kind of the highest precedence; the others must be combinable with it
        var owner = group.OrderByDescending(i => Precedence(maps[i][channel].Kind)).First();
        var ownerScale = maps[owner][channel];
        var members = new List<int>();
        foreach (var i in group)
        {
            if (i == owner || CanMerge(ownerScale, maps[i][channel]))
            {
                members.Add(i);
            }
            else
            {
                var own = maps[i][channel];
                warnings.Add(new FeatureReport(
                    "vl-layer-scale-conflict",
                    $"The {channel} scale of this layer is a {own.Kind} scale and cannot be shared with the {ownerScale.Kind} scale of another layer; the layer keeps its own scale.",
                    own.Location,
                    FeatureReportKind.Warning));
            }
        }

        if (members.Count < 2)
        {
            return null;
        }

        members.Sort();
        var ownScales = members.ToDictionary(i => i, i => maps[i][channel]);
        var combined = CombineDomains(channel, ownerScale, members.Select(i => ownScales[i]).ToList(), limits);
        var shared = ownerScale;
        if (combined is not null && contexts[owner] is { } ownerContext && ownerContext.Encoding?[channel] is { IsObject: true } definition
            && ResolveChannel(channel, definition, ownerContext with { Shared = combined }) is { } rebuilt)
        {
            shared = rebuilt;
        }

        foreach (var i in members)
        {
            maps[i][channel] = Bind(shared, ownScales[i]);
        }

        var titles = members.Select(i => ownScales[i].Title).Where(t => t is not null).Distinct(StringComparer.Ordinal).ToList();
        return new MergeResult(shared with { Title = titles.Count == 0 ? null : string.Join(", ", titles) }, members);
    }

    /// <summary>
    ///     Binds a shared scale to the field of one layer: everything about where values are mapped is the shared scale's, everything about which field is read is the layer's.
    /// </summary>
    /// <param name="shared">The shared scale.</param>
    /// <param name="own">The layer's own scale.</param>
    /// <returns>The scale the layer's marks use.</returns>
    private static ResolvedScale Bind(ResolvedScale shared, ResolvedScale own) => shared with
    {
        FieldPath = own.FieldPath,
        Field2Path = own.Field2Path,
        Title = own.Title,
        Bin = own.Bin,
        BinSpec = own.BinSpec,
        Stack = own.Stack,
        DataType = own.DataType,
        Location = own.Location,
        HasBandEnd = own.HasBandEnd,
        IsTemporalField = own.IsTemporalField,
        TimeUnit = own.TimeUnit,
    };

    /// <summary>
    ///     Tells whether the scale of a layer can be merged with the owner's: the same family of scale, and for continuous scales the same kind and transform.
    /// </summary>
    /// <param name="owner">The owner's scale.</param>
    /// <param name="other">The other layer's scale.</param>
    /// <returns><see langword="true"/> when the scales can be merged.</returns>
    private static bool CanMerge(ResolvedScale owner, ResolvedScale other)
    {
        static ScaleKind Normal(ScaleKind kind) => kind == ScaleKind.Diverging ? ScaleKind.Sequential : kind;
        static int Family(ScaleKind kind) => kind switch
        {
            ScaleKind.Band or ScaleKind.Point => 0,
            ScaleKind.Ordinal => 1,
            ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold or ScaleKind.BinOrdinal => 2,
            _ => 3,
        };

        var family = Family(owner.Kind);
        if (family != Family(other.Kind))
        {
            return false;
        }

        return family switch
        {
            0 or 1 => true,
            2 => owner.Kind == other.Kind,
            _ => Normal(owner.Kind) == Normal(other.Kind) && owner.Transform.Kind == other.Transform.Kind,
        };
    }

    /// <summary>
    ///     Combines the domains of the scales of the layers that share a scale.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="owner">The owner's scale.</param>
    /// <param name="scales">The scales of the members, in layer order.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The shared domain; null when the owner's scale is used as it is (the discretizing scales).</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the combined categories exceed <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private static SharedDomain? CombineDomains(string channel, ResolvedScale owner, List<ResolvedScale> scales, VegaLiteLimits limits)
    {
        if (owner.Kind is ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold or ScaleKind.BinOrdinal)
        {
            return null;
        }

        if (owner.Kind is ScaleKind.Band or ScaleKind.Point or ScaleKind.Ordinal)
        {
            return new SharedDomain(channel, 0, 0, null, CombineCategories(owner, scales, limits));
        }

        // Vega-Lite's mergeValuesWithExplicit: explicit domains win over implicit ones, which are then ignored; explicit domains that differ are all kept,
        // and Vega takes the extent of the union of their values (a piecewise domain is not kept). An explicit domain that equals another one counts once.
        var explicitScales = scales.Where(s => s.HasExplicitDomain).ToList();
        if (explicitScales.Count > 0)
        {
            var first = explicitScales[0];
            if (explicitScales.All(s => SameDomain(first, s)))
            {
                return new SharedDomain(channel, first.DomainStart, first.DomainEnd, first.Domain?.ToArray(), null);
            }

            var values = explicitScales.SelectMany(s => s.Domain ?? [s.DomainStart, s.DomainEnd]).ToList();
            return new SharedDomain(channel, values.Min(), values.Max(), null, null);
        }

        // An implicit domain of more than two entries (a diverging color scale, for one) is not combined: the first layer that has one decides
        var deciding = scales.FirstOrDefault(s => s.Domain is not null);
        if (deciding is not null)
        {
            return new SharedDomain(channel, deciding.DomainStart, deciding.DomainEnd, deciding.Domain?.ToArray(), null);
        }

        return new SharedDomain(channel, scales.Min(s => s.DomainStart), scales.Max(s => s.DomainEnd), null, null);
    }

    /// <summary>
    ///     Tells whether two continuous scales have the same domain (Vega-Lite's <c>deepEqual</c> of the domains).
    /// </summary>
    /// <param name="a">The first scale.</param>
    /// <param name="b">The second scale.</param>
    /// <returns><see langword="true"/> when the domains hold the same numbers in the same order.</returns>
    private static bool SameDomain(ResolvedScale a, ResolvedScale b) =>
        (a.Domain ?? [a.DomainStart, a.DomainEnd]).SequenceEqual(b.Domain ?? [b.DomainStart, b.DomainEnd]);

    /// <summary>
    ///     Combines the categories of discrete scales.
    /// </summary>
    /// <param name="owner">The owner's scale (the location of a limit error).</param>
    /// <param name="scales">The scales of the members, in layer order.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The categories: when any layer gives an explicit domain, the union of the explicit domains (every category once, in order of first appearance; implicit domains are ignored, as in Vega-Lite); otherwise every category once in layer order, sorted when every layer sorts the same way.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the combined categories exceed <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private static List<DataValue> CombineCategories(ResolvedScale owner, List<ResolvedScale> scales, VegaLiteLimits limits)
    {
        List<DataValue> categories;
        if (scales.Any(s => s.HasExplicitDomain))
        {
            var seenExplicit = new HashSet<DataValue>();
            categories = [.. scales.Where(s => s.HasExplicitDomain).SelectMany(s => s.DiscreteDomain).Where(seenExplicit.Add)];
        }
        else
        {
            var seen = new HashSet<DataValue>();
            categories = [];
            foreach (var value in scales.SelectMany(s => s.DiscreteDomain).Where(seen.Add))
            {
                categories.Add(value);
                if (categories.Count > limits.MaxScaleDomainSize)
                {
                    break;
                }
            }

            if (scales.All(s => s.DomainOrder == DomainOrder.Ascending))
            {
                categories = [.. categories.OrderBy(v => v, Comparer<DataValue>.Create(DataValue.Compare))];
            }
            else if (scales.All(s => s.DomainOrder == DomainOrder.Descending))
            {
                categories = [.. categories.OrderBy(v => v, Comparer<DataValue>.Create((x, y) => DataValue.Compare(y, x)))];
            }
        }

        if (categories.Count > limits.MaxScaleDomainSize)
        {
            throw new VegaLiteRenderException(
                $"The {owner.Channel} scale has more than {limits.MaxScaleDomainSize} categories, the limit (MaxScaleDomainSize).",
                owner.Location);
        }

        return categories;
    }
}
