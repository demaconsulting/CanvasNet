// cspell:ignore STZE
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     Validation of the properties that layers and composite marks add: <c>resolve</c>, and the properties of the <c>boxplot</c>, <c>errorbar</c> and
///     <c>errorband</c> mark definitions.
/// </content>
/// <remarks>
///     The same rule as for the rest of the specification applies: a value of the wrong shape is a <see cref="VegaLiteSpecException"/> at that value,
///     and a property Vega-Lite does not define is a <c>vl-unknown-property</c> warning. The checks need the whole tree (<c>resolve</c> may sit on any view)
///     but no data.
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The channels whose scales <c>resolve.scale</c> may name (Vega-Lite's <c>ScaleResolveMap</c>).</summary>
    private static readonly HashSet<string> ResolveScaleChannels = Set(
        "angle color fill fillOpacity opacity radius shape size stroke strokeDash strokeOpacity strokeWidth theta time x xOffset y yOffset");

    /// <summary>The channels whose axes <c>resolve.axis</c> may name (Vega-Lite's <c>AxisResolveMap</c>).</summary>
    private static readonly HashSet<string> ResolveAxisChannels = Set("x y");

    /// <summary>The channels whose legends <c>resolve.legend</c> may name (Vega-Lite's <c>LegendResolveMap</c>).</summary>
    private static readonly HashSet<string> ResolveLegendChannels = Set(
        "angle color fill fillOpacity opacity shape size stroke strokeDash strokeOpacity strokeWidth");

    /// <summary>The rules for the properties of a composite mark definition (<c>boxplot</c>, <c>errorbar</c>, <c>errorband</c>).</summary>
    private static readonly Dictionary<string, Rule> CompositeMarkRules = Rules(
        "extent: NS", "center: SE=mean|median", "box: BO", "median: BO", "rule: BO", "ticks: BO", "outliers: BO", "band: BO", "borders: BO");

    /// <summary>The properties that lay out the cells of a concatenation or a facet, on a view or on a <c>facet</c> channel definition.</summary>
    private static readonly string[] LayoutProperties = ["columns", "spacing", "align", "center"];

    /// <summary>The properties that make a view a concatenation, a facet or a repeat.</summary>
    private static readonly string[] CompositionKeys = ["concat", "hconcat", "vconcat", "facet", "repeat"];

    /// <summary>The names a <c>{"repeat": ...}</c> reference may have.</summary>
    private static readonly string[] RepeatReferenceNames = ["row", "column", "repeat", "layer"];

    /// <summary>The properties of a channel definition or a condition that may be a <c>{"repeat": ...}</c> reference.</summary>
    private static readonly string[] ReferenceProperties = ["field", "datum"];

    /// <summary>The rules for the properties of a <c>header</c> of a faceting channel definition (Vega-Lite's <c>Header</c>).</summary>
    private static readonly Dictionary<string, Rule> HeaderRules = Rules(
        "format: SO", "formatType: S", "labelAlign: SE", "labelAnchor: SE=start|middle|end", "labelAngle: NE", "labelBaseline: SE",
        "labelColor: SZE", "labelExpr: S", "labelFont: SE", "labelFontSize: NE", "labelFontStyle: SE", "labelFontWeight: SNE", "labelLimit: NE",
        "labelLineHeight: NE", "labelOrient: S=left|right|top|bottom", "labelPadding: NE", "labels: B", "orient: S=left|right|top|bottom",
        "title: STZE", "titleAlign: SE", "titleAnchor: SE=start|middle|end", "titleAngle: NE", "titleBaseline: SE", "titleColor: SZE",
        "titleFont: SE", "titleFontSize: NE", "titleFontStyle: SE", "titleFontWeight: SNE", "titleLimit: NE", "titleLineHeight: NE",
        "titleOrient: S=left|right|top|bottom", "titlePadding: NE");

    /// <summary>The extent names an <c>errorbar</c> or <c>errorband</c> accepts.</summary>
    private static readonly string[] ErrorExtentNames = ["ci", "iqr", "stderr", "stdev"];

    /// <summary>
    ///     Determines whether a name is a channel that a <c>resolve</c> of that kind may name.
    /// </summary>
    /// <param name="kind"><c>scale</c>, <c>axis</c> or <c>legend</c>.</param>
    /// <param name="channel">The channel name.</param>
    /// <returns><see langword="true"/> when Vega-Lite accepts the channel under that kind.</returns>
    public static bool IsResolveChannel(string kind, string channel) => kind switch
    {
        "scale" => ResolveScaleChannels.Contains(channel),
        "axis" => ResolveAxisChannels.Contains(channel),
        "legend" => ResolveLegendChannels.Contains(channel),
        _ => false,
    };

    /// <summary>
    ///     Validates the <c>resolve</c> of a view: an object whose <c>scale</c>, <c>axis</c> and <c>legend</c> map channels to <c>shared</c> or <c>independent</c>.
    /// </summary>
    /// <param name="resolve">The <c>resolve</c> value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a value that is not an object, a channel that the kind does not accept, or a mode other than <c>shared</c> or <c>independent</c>.</exception>
    private void ValidateResolve(SpecNode resolve)
    {
        if (!resolve.IsObject)
        {
            throw Error(resolve, "The 'resolve' must be an object.");
        }

        foreach (var kind in resolve.Properties)
        {
            if (kind.Name is not ("scale" or "axis" or "legend"))
            {
                WarnUnknown(kind, "a resolve");
                continue;
            }

            if (!kind.IsObject)
            {
                throw Error(kind, $"The 'resolve.{kind.Name}' must be an object that maps channels to 'shared' or 'independent'.");
            }

            foreach (var entry in kind.Properties)
            {
                if (!IsResolveChannel(kind.Name, entry.Name!))
                {
                    throw Error(entry, $"'{entry.Name}' is not a channel whose {kind.Name} can be resolved.");
                }

                if (entry.StringValue is not ("shared" or "independent"))
                {
                    throw Error(entry, $"The 'resolve.{kind.Name}.{entry.Name}' must be 'shared' or 'independent'.");
                }
            }
        }
    }

    /// <summary>
    ///     Validates the properties of a composite mark definition that the primitive marks do not have.
    /// </summary>
    /// <remarks>
    ///     <c>extent</c> is a number (a multiple of the interquartile range) or <c>min-max</c> for a boxplot, and one of <c>ci</c>, <c>iqr</c>,
    ///     <c>stderr</c> and <c>stdev</c> for an errorbar or errorband. The part properties (<c>box</c>, <c>median</c>, <c>rule</c>, <c>ticks</c>,
    ///     <c>outliers</c>, <c>band</c>, <c>borders</c>) are a boolean or an object with the mark properties of the part.
    /// </remarks>
    /// <param name="mark">The mark definition object.</param>
    /// <param name="type">The composite mark type.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a property of the wrong type or an extent the mark does not know.</exception>
    private void ValidateCompositeMark(SpecNode mark, string type)
    {
        CheckObject(mark, CompositeMarkRules);
        if (mark["extent"] is not { } extent)
        {
            return;
        }

        if (type == "boxplot")
        {
            if (extent.IsString && extent.StringValue != "min-max")
            {
                throw Error(extent, "The 'extent' of a boxplot must be a number or 'min-max'.");
            }

            if (extent.IsNumber && extent.NumberValue < 0)
            {
                throw Error(extent, "The 'extent' of a boxplot must not be negative.");
            }

            return;
        }

        if (!extent.IsString || Array.IndexOf(ErrorExtentNames, extent.StringValue) < 0)
        {
            throw Error(extent, $"The 'extent' of an {type} must be one of 'ci', 'iqr', 'stderr' or 'stdev'.");
        }
    }

    /// <summary>
    ///     Validates the views of a <c>layer</c>: each is a unit view or a layered view, never a concatenation, a facet or a repeat (Vega-Lite's schema).
    /// </summary>
    /// <param name="views">The views of the <c>layer</c> array.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the view, when a layer is a composition.</exception>
    private void ValidateLayerChildren(List<SpecNode> views)
    {
        foreach (var view in views)
        {
            if (CompositionKeys.FirstOrDefault(key => view[key] is not null) is { } key)
            {
                throw Error(view, $"A view in a 'layer' cannot be a '{key}' composition; only unit and layered views can be layered.");
            }
        }
    }

    /// <summary>
    ///     Validates the <c>spec</c> of a facet: a unit view or a layered view, never a concatenation, a facet or a repeat.
    /// </summary>
    /// <param name="spec">The <c>spec</c> value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the <c>spec</c>, when it is a composition and the view it is in is a facet.</exception>
    private void ValidateFacetSpec(SpecNode spec)
    {
        if (spec.Parent?["facet"] is null)
        {
            return;
        }

        if (CompositionKeys.FirstOrDefault(key => spec[key] is not null) is { } key)
        {
            throw Error(spec, $"The 'spec' of a facet cannot be a '{key}' composition; it must be a unit view or a layered view.");
        }
    }

    /// <summary>
    ///     Validates the <c>facet</c> of a view: one field definition (a wrapped facet), or an object with a <c>row</c> and a <c>column</c> field definition.
    /// </summary>
    /// <param name="facet">The <c>facet</c> object (its kind is already checked).</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, when the view has no <c>spec</c> or a field definition has no <c>field</c>.</exception>
    private void ValidateFacet(SpecNode facet)
    {
        if (facet.Parent?["spec"] is not { IsObject: true })
        {
            throw Error(facet, "A facet needs a 'spec' view.");
        }

        if (facet["field"] is not null)
        {
            ValidateFacetField(facet);
            return;
        }

        var any = false;
        foreach (var entry in facet.Properties)
        {
            if (entry.Name is "row" or "column")
            {
                ValidateFacetField(entry);
                any = true;
            }
            else
            {
                WarnUnknown(entry, "a facet");
            }
        }

        if (!any)
        {
            throw Error(facet, "A facet needs a 'field', or a 'row' or a 'column' field definition.");
        }
    }

    /// <summary>
    ///     Validates one field definition of a facet: an object with a <c>field</c> that is a name or a <c>{"repeat": ...}</c> reference, and the properties of a channel definition.
    /// </summary>
    /// <param name="definition">The field definition.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, when it is not an object, has no <c>field</c>, or a property has a wrong value.</exception>
    private void ValidateFacetField(SpecNode definition)
    {
        if (!definition.IsObject)
        {
            throw Error(definition, $"The '{definition.Name}' of a facet must be a field definition object.");
        }

        if (definition["field"] is not { } field || !(field.IsString || IsRepeatReference(field)))
        {
            throw Error(definition, $"The '{definition.Name}' field definition of a facet needs a 'field'.");
        }

        ValidateChannelDefinition(definition, facetChannel: true);
    }

    /// <summary>
    ///     Validates the <c>repeat</c> of a view: an array of field names, or an object with <c>row</c>, <c>column</c> and <c>layer</c> arrays of field names.
    /// </summary>
    /// <param name="repeat">The <c>repeat</c> value (its kind is already checked).</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, when the view has no <c>spec</c> or a value is not a string.</exception>
    private void ValidateRepeat(SpecNode repeat)
    {
        if (repeat.Parent?["spec"] is not { IsObject: true })
        {
            throw Error(repeat, "A repeat needs a 'spec' view.");
        }

        if (repeat.IsArray)
        {
            CheckStringItems(repeat, "The values of a 'repeat' must be strings.");
            return;
        }

        foreach (var entry in repeat.Properties)
        {
            if (entry.Name is not ("row" or "column" or "layer"))
            {
                WarnUnknown(entry, "a repeat");
                continue;
            }

            if (!entry.IsArray)
            {
                throw Error(entry, $"The 'repeat.{entry.Name}' must be an array of field names.");
            }

            CheckStringItems(entry, $"The values of 'repeat.{entry.Name}' must be strings.");
        }
    }

    /// <summary>
    ///     Validates what the rules cannot express of a layout property of a concatenation or a facet: <c>columns</c> is a non-negative integer, and <c>spacing</c>,
    ///     <c>align</c> and <c>center</c> are one value or an object that gives the row and the column separately.
    /// </summary>
    /// <param name="property">The property value, whose kind is already checked.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a value that is not allowed.</exception>
    private void ValidateLayoutProperty(SpecNode property)
    {
        switch (property.Name)
        {
            case "columns":
                if (property.NumberValue < 0 || !double.IsInteger(property.NumberValue))
                {
                    throw Error(property, "The 'columns' must be a non-negative integer.");
                }

                break;
            case "spacing" when property.IsObject:
                ValidateRowColumn(property, "spacing", n => n.IsNumber, "a number");
                break;
            case "align" when property.IsString:
                if (property.StringValue is not ("all" or "each" or "none"))
                {
                    throw Error(property, "The 'align' must be 'all', 'each' or 'none'.");
                }

                break;
            case "align" when property.IsObject:
                ValidateRowColumn(property, "align", n => n.IsString && n.StringValue is "all" or "each" or "none", "'all', 'each' or 'none'");
                break;
            case "center" when property.IsObject:
                ValidateRowColumn(property, "center", n => n.IsBoolean, "a boolean");
                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Validates an object that gives a value for the <c>row</c> and for the <c>column</c>.
    /// </summary>
    /// <param name="value">The object.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="isValid">Tells whether an entry has an allowed value.</param>
    /// <param name="expected">The description of the allowed values for the message.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the entry, when its value is not allowed.</exception>
    private void ValidateRowColumn(SpecNode value, string name, Func<SpecNode, bool> isValid, string expected)
    {
        foreach (var entry in value.Properties)
        {
            if (entry.Name is not ("row" or "column"))
            {
                WarnUnknown(entry, $"a {name} object");
            }
            else if (!isValid(entry))
            {
                throw Error(entry, $"The '{name}.{entry.Name}' must be {expected}.");
            }
        }
    }

    /// <summary>
    ///     Validates the <c>header</c> of a faceting channel definition (or of a facet field definition).
    /// </summary>
    /// <param name="header">The header object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a property of the wrong type or value.</exception>
    private void ValidateHeader(SpecNode header)
    {
        CheckObject(header, HeaderRules);
        foreach (var property in header.Properties.Where(p => !HeaderRules.ContainsKey(p.Name!)))
        {
            WarnUnknown(property, "a header");
        }
    }

    /// <summary>
    ///     Validates the <c>{"repeat": ...}</c> references of a channel definition: in its <c>field</c>, <c>datum</c> and <c>sort.field</c>, and in the
    ///     <c>field</c> and <c>datum</c> of its conditions, the reference names <c>row</c>, <c>column</c>, <c>repeat</c> or <c>layer</c>.
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the reference, when it names anything else.</exception>
    private void ValidateRepeatReferences(SpecNode definition)
    {
        ValidateReferencesOf(definition);
        if (definition["sort"] is { IsObject: true } sort && sort["field"] is { } sortField)
        {
            ValidateReference(sortField);
        }

        if (definition["condition"] is { } condition)
        {
            foreach (var item in condition.IsArray ? condition.Items : [condition])
            {
                if (item.IsObject)
                {
                    ValidateReferencesOf(item);
                }
            }
        }
    }

    /// <summary>
    ///     Validates the references in the <c>field</c> and the <c>datum</c> of an object.
    /// </summary>
    /// <param name="definition">The object.</param>
    private void ValidateReferencesOf(SpecNode definition)
    {
        foreach (var name in ReferenceProperties)
        {
            if (definition[name] is { } value)
            {
                ValidateReference(value);
            }
        }
    }

    /// <summary>
    ///     Validates a value that may be a <c>{"repeat": ...}</c> reference: an object with a <c>repeat</c> key must name <c>row</c>, <c>column</c>, <c>repeat</c> or <c>layer</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the <c>repeat</c> value, when it names anything else.</exception>
    private void ValidateReference(SpecNode value)
    {
        if (value.IsObject && value["repeat"] is { } name && (!name.IsString || Array.IndexOf(RepeatReferenceNames, name.StringValue) < 0))
        {
            throw Error(name, "The 'repeat' of a reference must be 'row', 'column', 'repeat' or 'layer'.");
        }
    }

    /// <summary>
    ///     Determines whether a value is a <c>{"repeat": ...}</c> reference.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns><see langword="true"/> for an object with a string <c>repeat</c>.</returns>
    private static bool IsRepeatReference(SpecNode node) => node.IsObject && node["repeat"] is { IsString: true };
}
