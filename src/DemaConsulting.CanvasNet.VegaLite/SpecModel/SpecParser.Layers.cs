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
}
