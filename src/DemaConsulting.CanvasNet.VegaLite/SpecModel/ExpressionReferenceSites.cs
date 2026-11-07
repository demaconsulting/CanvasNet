namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The objects of a specification that Vega-Lite's schema allows an <c>{"expr": ...}</c> reference at, other than the ones a predicate or a transform holds: the static sites.
/// </summary>
/// <remarks>
///     <para>
///         Vega-Lite compiles an <c>ExprRef</c> to a Vega signal, so its value is whatever the expression gives, evaluated once per view, with no row. The schema lists the properties
///         that take one (<c>ExprRef</c> in the definitions of the top-level specifications, titles, marks, axes, legends, headers, scales, view backgrounds, channel definitions and
///         the configuration); this class holds that list, owner by owner, and walks a specification along the properties that lead to those owners. The walk never enters <c>data</c>,
///         <c>datasets</c>, <c>transform</c>, <c>params</c>, <c>selection</c> or <c>projection</c>, and it does not read <c>config.selection</c>: a row of inline data can hold an object with an <c>expr</c> key, a predicate or a transform
///         has its own rules, and the schema (6.5.0: <c>BrushConfig</c>, <c>PointSelectionConfig</c>, <c>IntervalSelectionConfig</c>, <c>SelectionInit</c>) allows no <c>ExprRef</c> in a selection, which the feature catalog reports instead.
///     </para>
///     <para>
///         <c>width</c> and <c>height</c> are not on the list (Vega-Lite's size is a number, <c>"container"</c> or a step), and neither are the text properties that a schema
///         does not mark as an expression reference (<c>subtitle</c>, the <c>title</c> of an axis). The walk also reaches the sites that the marks evaluate for every row (the paint
///         properties of the mark, and the <c>value</c> of a color, fill, stroke, opacity or size channel); the specification parser compiled those under its own rules, and the caller
///         tells them apart by the compiled expression it already holds. The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ExpressionReferenceSites
{
    /// <summary>The kinds of object that carry expression references, by the way a walk reaches them.</summary>
    internal enum Owner
    {
        /// <summary>A view of the specification (the top-level specification or a nested one).</summary>
        View,

        /// <summary>The top-level specification: a view whose <c>background</c> and <c>padding</c> count and whose <c>config</c> is read.</summary>
        RootView,

        /// <summary>A title definition (<c>TitleParams</c>).</summary>
        Title,

        /// <summary>A mark definition, an overlay or a part of a composite mark, or a mark configuration.</summary>
        Mark,

        /// <summary>An <c>encoding</c>.</summary>
        Encoding,

        /// <summary>A channel definition, or the definition of a facet.</summary>
        ChannelDef,

        /// <summary>A condition of a channel: its <c>value</c> and <c>datum</c> are evaluated for every row by the mark that draws the channel, so only the guides and the scale of its field are static.</summary>
        Condition,

        /// <summary>An axis definition or an axis configuration.</summary>
        Axis,

        /// <summary>A legend definition or the legend configuration.</summary>
        Legend,

        /// <summary>A header definition or a header configuration.</summary>
        Header,

        /// <summary>A scale definition.</summary>
        Scale,

        /// <summary>The scale configuration.</summary>
        ScaleConfig,

        /// <summary>The background of a view (<c>view</c>, or <c>config.view</c>).</summary>
        ViewBackground,

        /// <summary>The configuration.</summary>
        Config,
    }

    /// <summary>One reference found by <see cref="Find"/>.</summary>
    /// <param name="Reference">The <c>{"expr": ...}</c> object.</param>
    /// <param name="Text">The string node that holds the expression.</param>
    /// <param name="Kind">How the value is read.</param>
    internal readonly record struct Site(SpecNode Reference, SpecNode Text, StaticSiteKind Kind);

    /// <summary>The properties of a title definition that take an expression reference.</summary>
    private static readonly HashSet<string> TitleProperties = Names(
        "anchor angle aria color dx dy font fontSize fontStyle fontWeight frame limit lineHeight offset orient subtitleColor subtitleFont subtitleFontSize subtitleFontStyle subtitleFontWeight "
        + "subtitleLineHeight subtitlePadding text zindex");

    /// <summary>The properties of a mark definition, an overlay, a part of a composite mark and a mark configuration that take an expression reference.</summary>
    private static readonly HashSet<string> MarkProperties = Names(
        "align angle aria ariaRole ariaRoleDescription aspect baseline blend clip color cornerRadius cornerRadiusBottomLeft cornerRadiusBottomRight cornerRadiusEnd cornerRadiusTopLeft "
        + "cornerRadiusTopRight cursor description dir dx dy ellipsis endAngle fill fillOpacity font fontSize fontStyle fontWeight height href innerRadius interpolate limit lineBreak "
        + "lineHeight minBandSize opacity outerRadius padAngle radius radius2 radius2Offset radiusOffset shape size smooth startAngle stroke strokeCap strokeDash strokeDashOffset "
        + "strokeJoin strokeMiterLimit strokeOffset strokeOpacity strokeWidth tension text theta theta2 theta2Offset thetaOffset time tooltip url width x x2 x2Offset xOffset y y2 "
        + "y2Offset yOffset");

    /// <summary>The properties of an axis definition and the axis configuration that take an expression reference.</summary>
    private static readonly HashSet<string> AxisProperties = Names(
        "aria bandPosition description domainCap domainColor domainDash domainDashOffset domainOpacity domainWidth gridCap gridColor gridDash gridDashOffset gridOpacity gridWidth "
        + "labelAlign labelAngle labelBaseline labelBound labelColor labelFlushOffset labelFont labelFontSize labelFontStyle labelFontWeight labelLimit labelLineHeight labelOffset "
        + "labelOpacity labelOverlap labelPadding labelSeparation maxExtent minExtent offset orient position tickBand tickCap tickColor tickCount tickDash tickDashOffset tickMinStep "
        + "tickOffset tickOpacity tickSize tickWidth titleAlign titleAnchor titleAngle titleBaseline titleColor titleFont titleFontSize titleFontStyle titleFontWeight titleLimit "
        + "titleLineHeight titleOpacity titlePadding titleX titleY translate values");

    /// <summary>The properties of a legend definition and the legend configuration that take an expression reference.</summary>
    private static readonly HashSet<string> LegendProperties = Names(
        "aria clipHeight columnPadding columns cornerRadius description fillColor gradientDirection gradientLabelLimit gradientLabelOffset gradientLength gradientOpacity "
        + "gradientStrokeColor gradientStrokeWidth gradientThickness gridAlign labelAlign labelBaseline labelColor labelFont labelFontSize labelFontStyle labelFontWeight labelLimit "
        + "labelOffset labelOpacity labelOverlap labelPadding labelSeparation layout legendX legendY offset padding rowPadding strokeColor strokeDash strokeWidth symbolBaseFillColor "
        + "symbolBaseStrokeColor symbolDash symbolDashOffset symbolDirection symbolFillColor symbolLimit symbolOffset symbolOpacity symbolSize symbolStrokeColor symbolStrokeWidth "
        + "symbolType tickCount tickMinStep titleAlign titleAnchor titleBaseline titleColor titleFont titleFontSize titleFontStyle titleFontWeight titleLimit titleLineHeight titleOpacity "
        + "titleOrient titlePadding values zindex");

    /// <summary>The properties of a header definition and a header configuration that take an expression reference.</summary>
    private static readonly HashSet<string> HeaderProperties = Names(
        "labelAlign labelBaseline labelColor labelFont labelFontSize labelFontStyle labelFontWeight labelLimit labelLineHeight labelPadding titleAlign titleBaseline titleColor titleFont "
        + "titleFontSize titleFontStyle titleFontWeight titleLimit titleLineHeight titlePadding");

    /// <summary>The properties of a scale definition that take an expression reference (as a whole; <c>domain</c> and <c>range</c> also take one for each entry).</summary>
    private static readonly HashSet<string> ScaleProperties = Names(
        "align base clamp constant domain domainMax domainMid domainMin domainRaw exponent interpolate nice padding paddingInner paddingOuter rangeMax rangeMin reverse round scheme zero");

    /// <summary>The properties of the scale configuration that take an expression reference.</summary>
    private static readonly HashSet<string> ScaleConfigProperties = Names(
        "bandPaddingInner bandPaddingOuter bandWithNestedOffsetPaddingInner bandWithNestedOffsetPaddingOuter barBandPaddingInner clamp continuousPadding offsetBandPaddingInner "
        + "offsetBandPaddingOuter pointPadding rectBandPaddingInner round tickBandPaddingInner xReverse");

    /// <summary>The properties of a view background and of the view configuration that take an expression reference.</summary>
    private static readonly HashSet<string> ViewBackgroundProperties = Names(
        "cornerRadius fill fillOpacity opacity stroke strokeCap strokeDash strokeDashOffset strokeJoin strokeMiterLimit strokeOpacity strokeWidth");

    /// <summary>The properties of the top-level specification and of the configuration that take an expression reference.</summary>
    private static readonly HashSet<string> ViewProperties = Names("background padding");

    /// <summary>The properties of the configuration that take an expression reference.</summary>
    private static readonly HashSet<string> ConfigProperties = Names("background lineBreak padding");

    /// <summary>The properties of a channel definition that take an expression reference.</summary>
    private static readonly HashSet<string> ChannelProperties = Names("value datum");

    /// <summary>The properties of a scale definition whose value is a domain: a date result is its milliseconds.</summary>
    private static readonly HashSet<string> DomainProperties = Names("domain domainMax domainMid domainMin domainRaw");

    /// <summary>The properties of a mark that are objects of the same kind: the overlays of a path mark and the parts of a composite mark.</summary>
    private static readonly HashSet<string> MarkParts = Names("point line box median rule ticks outliers band borders");

    /// <summary>The names under which the configuration holds a mark configuration (and the composite marks, whose parts are mark configurations).</summary>
    private static readonly HashSet<string> ConfigMarks = Names("mark area bar line rect tick circle point rule square text trail arc image geoshape boxplot errorbar errorband");

    /// <summary>The names under which the configuration holds a header configuration.</summary>
    private static readonly HashSet<string> ConfigHeaders = Names("header headerRow headerColumn headerFacet");

    /// <summary>The properties of a view that hold nested views as an array.</summary>
    private static readonly HashSet<string> ViewLists = Names("layer concat hconcat vconcat");

    /// <summary>
    ///     Gets the names of the properties of an owner that take an expression reference.
    /// </summary>
    /// <param name="owner">The kind of object.</param>
    /// <returns>The property names; empty for an owner that has no property of its own to list.</returns>
    public static IReadOnlySet<string> PropertiesOf(Owner owner) => owner switch
    {
        Owner.Title => TitleProperties,
        Owner.Mark => MarkProperties,
        Owner.Axis => AxisProperties,
        Owner.Legend => LegendProperties,
        Owner.Header => HeaderProperties,
        Owner.Scale => ScaleProperties,
        Owner.ScaleConfig => ScaleConfigProperties,
        Owner.ViewBackground => ViewBackgroundProperties,
        Owner.Config => ConfigProperties,
        Owner.RootView => ViewProperties,
        Owner.ChannelDef => ChannelProperties,
        _ => new HashSet<string>(),
    };

    /// <summary>
    ///     Finds the expression references of the static sites of a specification.
    /// </summary>
    /// <remarks>
    ///     The walk uses an explicit stack, so the nesting of the specification (bounded by the parser) cannot overflow the call stack. A reference is a leaf: what it holds is not searched.
    /// </remarks>
    /// <param name="root">The root of the specification.</param>
    /// <returns>The references in document order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    public static IReadOnlyList<Site> Find(SpecNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var sites = new List<Site>();
        var pending = new Stack<(SpecNode Node, Owner Owner)>();
        pending.Push((root, Owner.RootView));
        while (pending.Count > 0)
        {
            var (node, owner) = pending.Pop();
            if (!node.IsObject)
            {
                continue;
            }

            foreach (var property in node.Properties)
            {
                Visit(property, owner, sites, pending);
            }
        }

        return [.. sites.OrderBy(s => s.Text.Line).ThenBy(s => s.Text.Column)];
    }

    /// <summary>
    ///     Visits one property of an object: records the reference it is, or queues the object it leads to.
    /// </summary>
    /// <param name="property">The property value.</param>
    /// <param name="owner">The kind of the object that holds the property.</param>
    /// <param name="sites">Receives the references.</param>
    /// <param name="pending">Receives the objects to walk next.</param>
    private static void Visit(SpecNode property, Owner owner, List<Site> sites, Stack<(SpecNode Node, Owner Owner)> pending)
    {
        var name = property.Name!;
        if (owner is Owner.View or Owner.RootView)
        {
            VisitView(property, owner == Owner.RootView, sites, pending);
            return;
        }

        if (owner == Owner.Encoding)
        {
            QueueEach(property, Owner.ChannelDef, pending);
            return;
        }

        if (owner == Owner.Config)
        {
            VisitConfig(property, sites, pending);
            return;
        }

        if (PropertiesOf(owner).Contains(name))
        {
            Record(property, KindOf(owner, name), sites);
        }

        // The properties that lead on: the overlays and parts of a mark, the guides, scales, headers and conditions of a channel
        switch (owner)
        {
            case Owner.Mark when MarkParts.Contains(name):
                Queue(property, Owner.Mark, pending);
                break;
            case Owner.Scale when name is "domain" or "range" && property.IsArray:
                foreach (var item in property.Items.Where(IsReference))
                {
                    Record(item, name == "domain" ? StaticSiteKind.Domain : StaticSiteKind.Value, sites);
                }

                break;
            case Owner.ChannelDef or Owner.Condition:
                VisitChannel(property, pending);
                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Visits a property of a channel definition: the guides, the scale, the header and the conditions it leads to.
    /// </summary>
    /// <param name="property">The property value.</param>
    /// <param name="pending">Receives the objects to walk next.</param>
    private static void VisitChannel(SpecNode property, Stack<(SpecNode Node, Owner Owner)> pending)
    {
        switch (property.Name)
        {
            case "axis":
                Queue(property, Owner.Axis, pending);
                break;
            case "legend":
                Queue(property, Owner.Legend, pending);
                break;
            case "scale":
                Queue(property, Owner.Scale, pending);
                break;
            case "header":
                Queue(property, Owner.Header, pending);
                break;
            case "condition":
                QueueEach(property, Owner.Condition, pending);
                break;
            case "row" or "column":
                QueueEach(property, Owner.ChannelDef, pending);
                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Visits a property of a view.
    /// </summary>
    /// <param name="property">The property value.</param>
    /// <param name="isRoot">Whether the view is the top-level specification.</param>
    /// <param name="sites">Receives the references.</param>
    /// <param name="pending">Receives the objects to walk next.</param>
    private static void VisitView(SpecNode property, bool isRoot, List<Site> sites, Stack<(SpecNode Node, Owner Owner)> pending)
    {
        var name = property.Name!;
        if (isRoot && ViewProperties.Contains(name))
        {
            Record(property, StaticSiteKind.Value, sites);
            return;
        }

        switch (name)
        {
            case "title":
                Queue(property, Owner.Title, pending);
                break;
            case "mark":
                Queue(property, Owner.Mark, pending);
                break;
            case "encoding":
                Queue(property, Owner.Encoding, pending);
                break;
            case "view":
                Queue(property, Owner.ViewBackground, pending);
                break;
            case "config" when isRoot:
                Queue(property, Owner.Config, pending);
                break;
            case "facet":
                Queue(property, Owner.ChannelDef, pending);
                break;
            case "spec":
                Queue(property, Owner.View, pending);
                break;
            default:
                if (ViewLists.Contains(name))
                {
                    QueueEach(property, Owner.View, pending);
                }

                break;
        }
    }

    /// <summary>
    ///     Visits a property of the configuration.
    /// </summary>
    /// <param name="property">The property value.</param>
    /// <param name="sites">Receives the references.</param>
    /// <param name="pending">Receives the objects to walk next.</param>
    private static void VisitConfig(SpecNode property, List<Site> sites, Stack<(SpecNode Node, Owner Owner)> pending)
    {
        var name = property.Name!;
        if (ConfigProperties.Contains(name))
        {
            Record(property, StaticSiteKind.Value, sites);
        }
        else if (name == "title")
        {
            Queue(property, Owner.Title, pending);
        }
        else if (name == "legend")
        {
            Queue(property, Owner.Legend, pending);
        }
        else if (name == "view")
        {
            Queue(property, Owner.ViewBackground, pending);
        }
        else if (name == "scale")
        {
            Queue(property, Owner.ScaleConfig, pending);
        }
        else if (ConfigHeaders.Contains(name))
        {
            Queue(property, Owner.Header, pending);
        }
        else if (name.StartsWith("axis", StringComparison.Ordinal))
        {
            Queue(property, Owner.Axis, pending);
        }
        else if (ConfigMarks.Contains(name))
        {
            Queue(property, Owner.Mark, pending);
        }
        else if (name == "style" && property.IsObject)
        {
            foreach (var style in property.Properties)
            {
                Queue(style, Owner.Mark, pending);
            }
        }
    }

    /// <summary>
    ///     Records a property value when it is an expression reference.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="kind">How the value is read.</param>
    /// <param name="sites">Receives the reference.</param>
    private static void Record(SpecNode value, StaticSiteKind kind, List<Site> sites)
    {
        if (IsReference(value))
        {
            sites.Add(new Site(value, value["expr"]!, kind));
        }
    }

    /// <summary>
    ///     Tells how the value of a property is read.
    /// </summary>
    /// <param name="owner">The kind of object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The kind of site.</returns>
    private static StaticSiteKind KindOf(Owner owner, string name) => owner switch
    {
        Owner.Title or Owner.Mark when name == "text" => StaticSiteKind.Text,
        Owner.Scale when DomainProperties.Contains(name) => StaticSiteKind.Domain,
        _ => StaticSiteKind.Value,
    };

    /// <summary>
    ///     Tells whether a node is an expression reference: an object with a string <c>expr</c>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> for <c>{"expr": "..."}</c>.</returns>
    private static bool IsReference(SpecNode node) => node is { IsObject: true } && node["expr"] is { IsString: true };

    /// <summary>
    ///     Queues an object to be walked as an owner.
    /// </summary>
    /// <param name="node">The value; anything but an object is not queued.</param>
    /// <param name="owner">The kind of the object.</param>
    /// <param name="pending">The queue.</param>
    private static void Queue(SpecNode node, Owner owner, Stack<(SpecNode Node, Owner Owner)> pending)
    {
        if (node.IsObject && !IsReference(node))
        {
            pending.Push((node, owner));
        }
    }

    /// <summary>
    ///     Queues an object, or each object of an array, to be walked as an owner.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="owner">The kind of the objects.</param>
    /// <param name="pending">The queue.</param>
    private static void QueueEach(SpecNode node, Owner owner, Stack<(SpecNode Node, Owner Owner)> pending)
    {
        if (node.IsArray)
        {
            foreach (var item in node.Items)
            {
                Queue(item, owner, pending);
            }
        }
        else
        {
            Queue(node, owner, pending);
        }
    }

    /// <summary>
    ///     Creates a set of names from a space-separated list.
    /// </summary>
    /// <param name="names">The names separated by spaces.</param>
    /// <returns>The set (ordinal comparison).</returns>
    private static HashSet<string> Names(string names) =>
        new(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
}
