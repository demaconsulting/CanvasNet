using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     Validation of the time constructs of a specification: time units (a string or an object), date-time objects, and the
///     interval forms of <c>tickCount</c> and <c>nice</c>.
/// </content>
/// <remarks>
///     <para>
///         The rule is the one of the rest of the specification: a value whose shape cannot be what its author meant (a time
///         unit that does not exist, a month name that is not one, a <c>step</c> that is not positive, an interval that is not
///         one of the eight) is a <see cref="VegaLiteSpecException"/> at that exact value; a member that Vega-Lite does not define
///         is the warning <c>vl-unknown-property</c>. What the renderer does with a valid construct that it cannot apply (a
///         <c>binned</c> time unit) is the feature catalog's to report.
///     </para>
///     <para>
///         A date-time object is checked by <see cref="DateTimeObject.TryRead"/>, so every site that accepts one (a domain
///         entry, <c>domainMin</c> and <c>domainMax</c>, the <c>values</c> of an axis or a legend, the values of a predicate,
///         a <c>sort</c> array) reports the same errors at the same member.
///     </para>
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The time intervals that <c>tickCount</c> and <c>nice</c> accept (Vega-Lite's <c>TimeInterval</c>).</summary>
    private static readonly HashSet<string> TimeIntervals = Set("millisecond second minute hour day week month year");

    /// <summary>The keys of an interval object (<c>{"interval": "month", "step": 2}</c>); any other key is warned about.</summary>
    private static readonly HashSet<string> TimeIntervalKeys = Set("interval step");

    /// <summary>
    ///     Determines whether a name is one of the eight time intervals.
    /// </summary>
    /// <param name="name">The interval name.</param>
    /// <returns><see langword="true"/> for <c>millisecond</c>, <c>second</c>, <c>minute</c>, <c>hour</c>, <c>day</c>, <c>week</c>, <c>month</c> and <c>year</c>.</returns>
    public static bool IsTimeInterval(string name) => TimeIntervals.Contains(name);

    /// <summary>
    ///     Validates a <c>timeUnit</c>: null, a unit name (<c>yearmonth</c>, <c>utcmonthdate</c>, <c>binnedyearmonth</c>) or an object with
    ///     <c>unit</c>, <c>maxbins</c>, <c>step</c>, <c>utc</c> and <c>binned</c>.
    /// </summary>
    /// <param name="node">The <c>timeUnit</c> value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a unit that is not a time unit, a <c>step</c> or <c>maxbins</c> that is not a positive number, a flag that is not a boolean, or an object that names neither a <c>unit</c> nor <c>maxbins</c>.</exception>
    private void ValidateTimeUnit(SpecNode node)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.Null:
                return;
            case SpecNodeKind.String:
                if (!TimeUnitSpec.TryParse(node.StringValue!, out _, out _))
                {
                    throw Error(node, $"Unrecognized time unit '{node.StringValue}'.");
                }

                return;
            case SpecNodeKind.Object:
                ValidateTimeUnitObject(node);
                return;
            default:
                throw Error(node, "The 'timeUnit' must be a time unit name or an object.");
        }
    }

    /// <summary>
    ///     Validates the members of a time unit object.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a member with a wrong value, or an object without <c>unit</c> and <c>maxbins</c>.</exception>
    private void ValidateTimeUnitObject(SpecNode node)
    {
        foreach (var member in node.Properties)
        {
            switch (member.Name)
            {
                case "unit":
                    if (!member.IsString || !TimeUnitSpec.TryParse(member.StringValue!, out _, out _))
                    {
                        throw Error(member, member.IsString ? $"Unrecognized time unit '{member.StringValue}'." : "The 'unit' of a time unit must be a string.");
                    }

                    break;
                case "step" or "maxbins":
                    if (!member.IsNumber || !(member.NumberValue > 0))
                    {
                        throw Error(member, $"The '{member.Name}' of a time unit must be a positive number.");
                    }

                    break;
                case "utc" or "binned":
                    if (!member.IsBoolean)
                    {
                        throw Error(member, $"The '{member.Name}' of a time unit must be true or false.");
                    }

                    break;
                default:
                    WarnUnknown(member, "a time unit");
                    break;
            }
        }

        if (node["unit"] is null && node["maxbins"] is null)
        {
            throw Error(node, "A time unit object needs a 'unit' or a 'maxbins'.");
        }
    }

    /// <summary>
    ///     Validates a value that may be a date-time object: an object other than an expression reference must be one.
    /// </summary>
    /// <param name="node">The value; anything that is not an object is left to the caller.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending member, for a date-time object with an unknown key, a wrong kind of value, or a month, quarter or day that is not one.</exception>
    private void ValidateDateTimeValue(SpecNode node)
    {
        if (!node.IsObject || node["expr"] is { IsString: true })
        {
            return;
        }

        if (node.Properties.Count == 0)
        {
            throw Error(node, "A date-time object needs at least one of year, quarter, month, date, day, hours, minutes, seconds or milliseconds.");
        }

        if (!DateTimeObject.TryRead(node, out _, out var error, out var at))
        {
            throw Error(at ?? node, error ?? "The value is not a date-time object.");
        }
    }

    /// <summary>
    ///     Validates the objects among the items of an array (a domain, the <c>values</c> of a guide) as date-time objects.
    /// </summary>
    /// <param name="array">The array.</param>
    private void ValidateDateTimeItems(SpecNode array)
    {
        foreach (var item in array.Items)
        {
            ValidateDateTimeValue(item);
        }
    }

    /// <summary>
    ///     Validates a <c>tickCount</c> or <c>nice</c> that is not a number or a boolean: a time interval name, or an object with an <c>interval</c> and a <c>step</c>.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="name">The property name, for the message.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a text that is not one of the eight intervals, an object without a valid <c>interval</c>, or a <c>step</c> that is not a positive number.</exception>
    private void ValidateTimeInterval(SpecNode node, string name)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.String:
                if (!TimeIntervals.Contains(node.StringValue!))
                {
                    throw Error(node, $"The '{name}' text must be one of {string.Join(", ", TimeIntervals.Order(StringComparer.Ordinal).Select(v => "'" + v + "'"))}.");
                }

                return;
            case SpecNodeKind.Object when node["expr"] is { IsString: true }:
                return;
            case SpecNodeKind.Object:
                ValidateIntervalObject(node, name);
                return;
            default:
                return;
        }
    }

    /// <summary>
    ///     Validates an interval object (<c>{"interval": "month", "step": 2}</c>).
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="name">The property name, for the message.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the <c>interval</c> is missing or not one of the eight, or the <c>step</c> is not a positive number.</exception>
    private void ValidateIntervalObject(SpecNode node, string name)
    {
        if (node["interval"] is not { IsString: true } interval || !TimeIntervals.Contains(interval.StringValue!))
        {
            throw Error(node["interval"] ?? node, $"The 'interval' of the '{name}' object must be one of {string.Join(", ", TimeIntervals.Order(StringComparer.Ordinal).Select(v => "'" + v + "'"))}.");
        }

        if (node["step"] is { } step && !(step.IsNumber && step.NumberValue > 0))
        {
            throw Error(step, $"The 'step' of the '{name}' object must be a positive number.");
        }

        foreach (var property in node.Properties.Where(p => !TimeIntervalKeys.Contains(p.Name!)))
        {
            WarnUnknown(property, "a time interval");
        }
    }

    /// <summary>
    ///     Validates the time constructs of a scale: the interval forms of <c>nice</c>, and the date-time objects of <c>domain</c>, <c>domainMin</c> and <c>domainMax</c>.
    /// </summary>
    /// <param name="scale">The scale object, whose property kinds have been checked.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for an invalid interval or date-time object.</exception>
    private void ValidateScaleTime(SpecNode scale)
    {
        if (scale["nice"] is { } nice)
        {
            ValidateTimeInterval(nice, "nice");
        }

        if (scale["domain"] is { IsArray: true } domain)
        {
            ValidateDateTimeItems(domain);
        }

        foreach (var name in new[] { "domainMin", "domainMax" })
        {
            if (scale[name] is { } bound)
            {
                ValidateDateTimeValue(bound);
            }
        }
    }

    /// <summary>
    ///     Validates the time constructs of an axis or a legend: the interval forms of <c>tickCount</c> and the date-time objects among the <c>values</c>.
    /// </summary>
    /// <param name="guide">The axis or legend object, whose property kinds have been checked.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for an invalid interval or date-time object.</exception>
    private void ValidateGuideTime(SpecNode guide)
    {
        if (guide["tickCount"] is { } tickCount)
        {
            ValidateTimeInterval(tickCount, "tickCount");
        }

        if (guide["values"] is { IsArray: true } values)
        {
            ValidateDateTimeItems(values);
        }
    }

    /// <summary>
    ///     Validates the <c>datum</c> of a channel definition when it is a date-time object (an object that has a date-time key).
    /// </summary>
    /// <param name="definition">The channel definition or condition object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending member, for an invalid date-time object.</exception>
    private void ValidateDatum(SpecNode definition)
    {
        if (definition["datum"] is { IsObject: true } datum && IsDateTimeShaped(datum))
        {
            ValidateDateTimeValue(datum);
        }
    }

    /// <summary>
    ///     Tells whether an object has a key of a date-time object, which is how a <c>datum</c> that is one is told from a repeat reference or an expression.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <returns><see langword="true"/> when a key is <c>year</c>, <c>quarter</c>, <c>month</c>, <c>date</c>, <c>day</c>, <c>hours</c>, <c>minutes</c>, <c>seconds</c>, <c>milliseconds</c> or <c>utc</c>.</returns>
    public static bool IsDateTimeShaped(SpecNode node) => node.IsObject && node.Properties.Any(p => DateTimeKeys.Contains(p.Name!));

    /// <summary>
    ///     Validates the values that a field predicate compares with: the date-time objects, and the size of a membership list that is written as an expression.
    /// </summary>
    /// <param name="predicate">The predicate object.</param>
    /// <param name="comparison">The operator value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for an invalid time unit or date-time object, or a membership list longer than <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private void ValidatePredicateTime(SpecNode predicate, SpecNode comparison)
    {
        if (predicate["timeUnit"] is { } timeUnit)
        {
            ValidateTimeUnit(timeUnit);
        }

        ValidateDateTimeValue(comparison);
        ValidateDateTimeItems(comparison);
        if (comparison.Name == "oneOf" && PredicateSource.IsTimeAware(predicate) && comparison.Items.Count > _limits.MaxScaleDomainSize)
        {
            throw Error(comparison, $"The 'oneOf' of a predicate lists {comparison.Items.Count} values, more than the limit of {_limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).");
        }
    }

    /// <summary>
    ///     Compiles the expression of a field predicate that is written with the date and time functions, once, under the predicate object.
    /// </summary>
    /// <param name="predicate">The predicate object, validated.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for an expression that is longer than <see cref="VegaLiteLimits.MaxExpressionLength"/>, or that refers to a parameter that is not declared.</exception>
    private void CompileTimePredicate(SpecNode predicate)
    {
        if (PredicateSource.IsTimeAware(predicate) && PredicateSource.TryBuild(predicate, out var source))
        {
            CompileSource(predicate, source, predicate);
        }
    }

    /// <summary>
    ///     Validates a <c>timeUnit</c> transform: <c>{"timeUnit": "yearmonth", "field": "date", "as": "month"}</c>.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The <c>timeUnit</c> value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the unit is not valid, the <c>field</c> is not a field path, or the <c>as</c> is not a non-empty text.</exception>
    private void ValidateTimeUnitTransform(SpecNode entry, SpecNode operation)
    {
        if (operation.IsNull)
        {
            throw Error(operation, "The timeUnit of a transform must be a time unit name or an object.");
        }

        ValidateTimeUnit(operation);
        if (entry["field"] is not { } field)
        {
            throw Error(entry, "The timeUnit transform needs the 'field' to read.");
        }

        RequireFieldPath(field, "The 'field' of a timeUnit transform must be a valid field name.");
        RequireOutputName(entry, "as", "The timeUnit transform needs a text 'as' that names the new field.");
    }
}
