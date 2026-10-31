using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     Validation of the aggregating transforms (<c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>,
///     <c>stack</c>, <c>impute</c>) and of the encoding properties that aggregate, bin, stack and sort.
/// </content>
/// <remarks>
///     <para>
///         The rule is the one of the rest of the specification: a value whose shape cannot be what its author meant (an
///         operation that does not exist, a field that is not a path, a frame that is not a pair of integers, a bin step
///         that is not positive) is a <see cref="VegaLiteSpecException"/> at that exact value, with the JSON pointer, the line
///         and the column. Names that Vega-Lite defines but that the renderer cannot yet apply (a bin extent that is a
///         parameter, an encoding <c>sort</c> by an operation the scale cannot compute) are not errors here: the feature
///         catalog reports them.
///     </para>
///     <para>
///         Optional properties that Vega-Lite requires (the <c>as</c> of a bin or a stack, the <c>groupby</c> of a stack) are accepted
///         when absent and defaulted by the stage that computes the transform. The parameters of the window operations
///         <c>ntile</c> and <c>nth_value</c> are required, because without them the operation has no meaning.
///     </para>
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The stack offsets of Vega-Lite.</summary>
    private static readonly HashSet<string> StackOffsetNames = Set("zero center normalize");

    /// <summary>The imputation methods of Vega-Lite.</summary>
    private static readonly HashSet<string> ImputeMethods = Set("value mean median max min");

    /// <summary>The channels a <c>sort</c> can name (the encoding channels that carry a scale of their own).</summary>
    private static readonly HashSet<string> SortChannels = Set("x y color fill stroke strokeWidth size shape fillOpacity strokeOpacity opacity text");

    /// <summary>The sort orders.</summary>
    private static readonly HashSet<string> SortOrders = Set("ascending descending");

    /// <summary>The properties of a <c>sort</c> object; any other key is warned about.</summary>
    private static readonly HashSet<string> SortObjectKeys = Set("field op order encoding");

    /// <summary>The keys of a Vega-Lite date-time object (an item of the <c>sort</c> array of a temporal field).</summary>
    private static readonly HashSet<string> DateTimeKeys = Set("year quarter month date day hours minutes seconds milliseconds utc");

    /// <summary>The most numbers a bin <c>steps</c> array may have.</summary>
    private const int MaxBinSteps = 1024;

    /// <summary>The most numbers a bin <c>divide</c> array may have.</summary>
    private const int MaxBinDivisions = 16;

    /// <summary>
    ///     Determines whether a name is one of the channels an encoding <c>sort</c> can sort by.
    /// </summary>
    /// <param name="name">The channel name.</param>
    /// <returns><see langword="true"/> for <c>x</c>, <c>y</c>, <c>color</c>, <c>fill</c>, <c>stroke</c>, <c>strokeWidth</c>, <c>size</c>, <c>shape</c>, <c>fillOpacity</c>, <c>strokeOpacity</c>, <c>opacity</c> and <c>text</c>.</returns>
    public static bool IsSortChannel(string name) => SortChannels.Contains(name);

    /// <summary>
    ///     Splits the channel shorthand of a <c>sort</c> (<c>"y"</c> or <c>"-y"</c>) into its channel and direction.
    /// </summary>
    /// <param name="text">The text of the sort.</param>
    /// <param name="channel">Receives the channel.</param>
    /// <param name="descending">Receives whether the sort is descending (a leading minus sign).</param>
    /// <returns><see langword="true"/> when the text names a sort channel.</returns>
    public static bool TryParseSortShorthand(string? text, out string channel, out bool descending)
    {
        channel = string.Empty;
        descending = false;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        descending = text[0] == '-';
        channel = descending ? text[1..] : text;
        return SortChannels.Contains(channel);
    }

    /// <summary>
    ///     Reads the channel that a <c>sort</c> refers to: the channel shorthand (<c>"-y"</c>) or the <c>encoding</c> of a sort object.
    /// </summary>
    /// <param name="sort">The <c>sort</c> value.</param>
    /// <param name="channel">Receives the channel name.</param>
    /// <returns><see langword="true"/> when the sort refers to a channel.</returns>
    public static bool TryGetSortChannel(SpecNode sort, out string channel)
    {
        ArgumentNullException.ThrowIfNull(sort);
        channel = string.Empty;
        if (sort.IsString)
        {
            return TryParseSortShorthand(sort.StringValue, out channel, out _);
        }

        if (sort.IsObject && sort["encoding"] is { IsString: true } encoding)
        {
            channel = encoding.StringValue!;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Determines whether the renderer can apply the <c>sort</c> of a channel definition: the form is one it computes and, for a
    ///     sort by another channel, that channel exists and reads a field.
    /// </summary>
    /// <remarks>
    ///     Ascending, descending, null and an array of values are always supported. A channel shorthand (<c>"-y"</c>) or an object
    ///     with an <c>encoding</c> needs that channel in the same encoding with a <c>field</c> or an <c>aggregate</c>. An object with an
    ///     <c>op</c> needs one that can order categories (<see cref="AggregateMeasures.CanOrderCategories(AggregateOp)"/>: not
    ///     <c>values</c>, <c>exponential</c> or <c>exponentialb</c>). The shape itself was validated when the specification was parsed.
    /// </remarks>
    /// <param name="sort">The <c>sort</c> value.</param>
    /// <param name="definition">The channel definition that owns it.</param>
    /// <returns><see langword="true"/> when the sort is applied; false when the feature catalog must report it.</returns>
    public static bool IsSupportedSort(SpecNode sort, SpecNode definition)
    {
        ArgumentNullException.ThrowIfNull(sort);
        ArgumentNullException.ThrowIfNull(definition);
        switch (sort.Kind)
        {
            case SpecNodeKind.String:
                return sort.StringValue is "ascending" or "descending"
                    || (TryParseSortShorthand(sort.StringValue, out var channel, out _) && SortChannelReadsField(definition, channel));
            case SpecNodeKind.Object:
                if (sort["encoding"] is { IsString: true } encoding)
                {
                    return SortChannelReadsField(definition, encoding.StringValue!);
                }

                return sort["op"] is not { IsString: true } op || (AggregateMeasures.TryParse(op.StringValue, out var parsed) && AggregateMeasures.CanOrderCategories(parsed));
            default:
                return true;
        }
    }

    /// <summary>
    ///     Determines whether the encoding that holds a channel definition has another channel that reads a field or aggregates.
    /// </summary>
    /// <param name="definition">The channel definition.</param>
    /// <param name="channel">The other channel.</param>
    /// <returns><see langword="true"/> when the channel is an object with a <c>field</c> or an <c>aggregate</c>.</returns>
    private static bool SortChannelReadsField(SpecNode definition, string channel) =>
        definition.Parent is { IsObject: true } encoding
        && encoding[channel] is { IsObject: true } other
        && (other["field"] is { IsString: true } || other["aggregate"] is { IsNull: false });

    // ------------------------------------------------------------------------------------------------------------------
    // Transforms
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Validates an <c>aggregate</c> or <c>joinaggregate</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The array of measures.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateAggregateTransform(SpecNode entry, SpecNode operation)
    {
        var name = operation.Name!;
        ValidateMeasures(operation, false, $"The {name} must be an array of {{op, field, as}} objects.");
        if (entry["groupby"] is { } groupBy)
        {
            ValidateFieldList(groupBy, $"The 'groupby' of {name} must be an array of field names.");
        }
    }

    /// <summary>
    ///     Validates a <c>window</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The array of window operations.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateWindowTransform(SpecNode entry, SpecNode operation)
    {
        ValidateMeasures(operation, true, "The window must be an array of {op, field, param, as} objects.");
        if (entry["groupby"] is { } groupBy)
        {
            ValidateFieldList(groupBy, "The 'groupby' of a window must be an array of field names.");
        }

        if (entry["sort"] is { } sort)
        {
            ValidateSortFields(sort, "The 'sort' of a window must be an array of {field, order} objects.");
        }

        if (entry["frame"] is { } frame)
        {
            ValidateFrame(frame, "The 'frame' of a window must be an array of two values, each an integer or null.");
        }

        if (entry["ignorePeers"] is { } ignorePeers && !ignorePeers.IsBoolean)
        {
            throw Error(ignorePeers, "The 'ignorePeers' of a window must be true or false.");
        }
    }

    /// <summary>
    ///     Validates the array of operations of an aggregate, joinaggregate or window transform.
    /// </summary>
    /// <param name="list">The array.</param>
    /// <param name="window">Whether the operations are those of a window (the window-only operations are accepted too).</param>
    /// <param name="message">The description for a value that is not an array of objects.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for the first entry that is not valid.</exception>
    private void ValidateMeasures(SpecNode list, bool window, string message)
    {
        if (!list.IsArray)
        {
            throw Error(list, message);
        }

        foreach (var item in list.Items)
        {
            if (!item.IsObject)
            {
                throw Error(item, message);
            }

            ValidateMeasure(item, window);
        }
    }

    /// <summary>
    ///     Validates one operation of an aggregate, joinaggregate or window transform.
    /// </summary>
    /// <param name="item">The object <c>{op, field, param, as}</c>.</param>
    /// <param name="window">Whether the window-only operations are accepted.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the operation is unknown, lacks a field it needs, or has a wrong parameter.</exception>
    private void ValidateMeasure(SpecNode item, bool window)
    {
        if (item["op"] is not { IsString: true } op)
        {
            throw Error(item["op"] ?? item, "An operation needs a text 'op'.");
        }

        var name = op.StringValue!;
        var needsField = true;
        WindowOp? windowOp = null;
        AggregateOp? aggregateOp = null;
        if (window && AggregateMeasures.TryParseWindow(name, out var parsedWindow))
        {
            windowOp = parsedWindow;
            needsField = AggregateMeasures.RequiresField(parsedWindow);
        }
        else if (AggregateMeasures.TryParse(name, out var parsedAggregate))
        {
            aggregateOp = parsedAggregate;
            needsField = AggregateMeasures.RequiresField(parsedAggregate);
        }
        else
        {
            throw Error(op, $"'{name}' is not {(window ? "an aggregate or window" : "an aggregate")} operation.");
        }

        if (item["field"] is { } field)
        {
            RequireFieldPath(field, "The 'field' of an operation must be a valid field name.");
        }
        else if (needsField)
        {
            throw Error(item, $"The '{name}' operation needs a 'field'.");
        }

        if (item["as"] is { } output && (!output.IsString || output.StringValue!.Length == 0))
        {
            throw Error(output, "The 'as' of an operation must be a non-empty text.");
        }

        if (item["param"] is { } parameter)
        {
            ValidateMeasureParameter(item, parameter, windowOp, aggregateOp);
        }
        else if (windowOp is WindowOp.Ntile or WindowOp.NthValue)
        {
            throw Error(item, $"The '{name}' operation needs a 'param'.");
        }
    }

    /// <summary>
    ///     Validates the <c>param</c> of an operation against the operation.
    /// </summary>
    /// <param name="item">The operation object.</param>
    /// <param name="parameter">The <c>param</c> value.</param>
    /// <param name="windowOp">The window-only operation, or null.</param>
    /// <param name="aggregateOp">The aggregate operation, or null.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the parameter is not valid for the operation.</exception>
    private void ValidateMeasureParameter(SpecNode item, SpecNode parameter, WindowOp? windowOp, AggregateOp? aggregateOp)
    {
        if (aggregateOp is { } aggregate && AggregateMeasures.IsExponential(aggregate))
        {
            if (!parameter.IsNumber || parameter.NumberValue <= 0 || parameter.NumberValue > 1)
            {
                throw Error(parameter, $"The 'param' of {item["op"]!.StringValue} must be a number greater than 0 and at most 1.");
            }

            return;
        }

        if (windowOp is { } window && AggregateMeasures.TakesParameter(window) && !IsInteger(parameter, 1, int.MaxValue))
        {
            throw Error(parameter, $"The 'param' of {item["op"]!.StringValue} must be an integer from 1 to {int.MaxValue}.");
        }

        // The other operations take no parameter; Vega-Lite lets it be written and the renderer ignores it
    }

    /// <summary>
    ///     Validates a <c>bin</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The <c>bin</c> value: true or the bin parameters.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateBinTransform(SpecNode entry, SpecNode operation)
    {
        if (operation is { IsBoolean: true } && operation.BoolValue)
        {
            // The default parameters
        }
        else if (operation.IsObject)
        {
            ValidateBinParameters(operation);
        }
        else
        {
            throw Error(operation, "The bin of a transform must be true or an object of bin parameters.");
        }

        if (entry["field"] is not { } field)
        {
            throw Error(entry, "The bin transform needs a 'field'.");
        }

        RequireFieldPath(field, "The 'field' of a bin must be a valid field name.");
        if (entry["as"] is { } output)
        {
            if (output.IsString)
            {
                if (output.StringValue!.Length == 0)
                {
                    throw Error(output, "The 'as' of a bin must be a non-empty text or an array of two of them.");
                }
            }
            else
            {
                ValidateOutputNames(output, 2, 2, "The 'as' of a bin must be a non-empty text or an array of two of them.");
            }
        }
    }

    /// <summary>
    ///     Validates the parameters of a <c>bin</c> (the object of a bin transform or of the <c>bin</c> of an encoding).
    /// </summary>
    /// <param name="bin">The object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for the first parameter that is out of range or of the wrong kind.</exception>
    private void ValidateBinParameters(SpecNode bin)
    {
        foreach (var parameter in bin.Properties)
        {
            switch (parameter.Name)
            {
                case "maxbins":
                    RequireNumber(parameter, 2, double.PositiveInfinity, "The 'maxbins' of a bin must be a number of at least 2.");
                    break;
                case "step":
                    RequireNumber(parameter, double.Epsilon, double.PositiveInfinity, "The 'step' of a bin must be a number greater than 0.");
                    break;
                case "steps":
                    RequireNumberList(parameter, MaxBinSteps, $"The 'steps' of a bin must be an array of 1 to {MaxBinSteps} numbers greater than 0.");
                    break;
                case "minstep":
                    RequireNumber(parameter, 0, double.PositiveInfinity, "The 'minstep' of a bin must be a number that is not negative.");
                    break;
                case "divide":
                    RequireNumberList(parameter, MaxBinDivisions, $"The 'divide' of a bin must be an array of 1 to {MaxBinDivisions} numbers greater than 0.");
                    break;
                case "base":
                    if (!parameter.IsNumber || parameter.NumberValue <= 1)
                    {
                        throw Error(parameter, "The 'base' of a bin must be a number greater than 1.");
                    }

                    break;
                case "anchor":
                    if (!parameter.IsNumber)
                    {
                        throw Error(parameter, "The 'anchor' of a bin must be a number.");
                    }

                    break;
                case "nice" or "binned":
                    if (!parameter.IsBoolean)
                    {
                        throw Error(parameter, $"The '{parameter.Name}' of a bin must be true or false.");
                    }

                    break;
                case "extent":
                    ValidateBinExtent(parameter);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    ///     Validates the <c>extent</c> of a bin: two numbers in order, or a reference to a parameter.
    /// </summary>
    /// <param name="extent">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is neither.</exception>
    private void ValidateBinExtent(SpecNode extent)
    {
        if (extent is { IsObject: true } && extent["param"] is { IsString: true })
        {
            // A parameter extent is recognized and reported by the feature catalog
            return;
        }

        if (!extent.IsArray || extent.Items.Count != 2 || extent.Items.Any(i => !i.IsNumber) || extent.Items[0].NumberValue > extent.Items[1].NumberValue)
        {
            throw Error(extent, "The 'extent' of a bin must be an array of two numbers in ascending order, or an object with a 'param'.");
        }
    }

    /// <summary>
    ///     Validates a <c>stack</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field that is stacked.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateStackTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The stack must be the name of the field to stack.");
        if (entry["groupby"] is { } groupBy)
        {
            ValidateFieldList(groupBy, "The 'groupby' of a stack must be an array of field names.");
        }

        if (entry["offset"] is { } offset && (!offset.IsString || !StackOffsetNames.Contains(offset.StringValue!)))
        {
            throw Error(offset, "The 'offset' of a stack must be 'zero', 'center' or 'normalize'.");
        }

        if (entry["sort"] is { } sort)
        {
            ValidateSortFields(sort, "The 'sort' of a stack must be an array of {field, order} objects.");
        }

        if (entry["as"] is { } output)
        {
            if (output.IsString)
            {
                if (output.StringValue!.Length == 0)
                {
                    throw Error(output, "The 'as' of a stack must be a non-empty text or an array of two of them.");
                }
            }
            else
            {
                ValidateOutputNames(output, 2, 2, "The 'as' of a stack must be a non-empty text or an array of two of them.");
            }
        }
    }

    /// <summary>
    ///     Validates an <c>impute</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field that is imputed.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateImputeTransform(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The impute must be the name of the field to impute.");
        if (entry["key"] is not { } key)
        {
            throw Error(entry, "The impute transform needs a 'key' field.");
        }

        RequireFieldPath(key, "The 'key' of an impute must be a valid field name.");
        if (entry["keyvals"] is { } keyValues)
        {
            ValidateKeyValues(keyValues);
        }

        if (entry["method"] is { } method && (!method.IsString || !ImputeMethods.Contains(method.StringValue!)))
        {
            throw Error(method, "The 'method' of an impute must be 'value', 'mean', 'median', 'max' or 'min'.");
        }

        if (entry["value"] is { } value && (value.IsObject || value.IsArray))
        {
            throw Error(value, "The 'value' of an impute must be a text, a number, a boolean or null.");
        }

        if (entry["groupby"] is { } groupBy)
        {
            ValidateFieldList(groupBy, "The 'groupby' of an impute must be an array of field names.");
        }

        if (entry["frame"] is { } frame)
        {
            ValidateFrame(frame, "The 'frame' of an impute must be an array of two values, each an integer or null.");
        }
    }

    /// <summary>
    ///     Validates the <c>keyvals</c> of an impute: an array of scalars or a sequence <c>{start, stop, step}</c>.
    /// </summary>
    /// <param name="keyValues">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is neither.</exception>
    private void ValidateKeyValues(SpecNode keyValues)
    {
        const string message = "The 'keyvals' of an impute must be an array of values or an object {start, stop, step}.";
        if (keyValues.IsArray)
        {
            if (keyValues.Items.Any(i => i.IsObject || i.IsArray))
            {
                throw Error(keyValues.Items.First(i => i.IsObject || i.IsArray), "A key value must be a text, a number, a boolean or null.");
            }

            return;
        }

        if (!keyValues.IsObject)
        {
            throw Error(keyValues, message);
        }

        if (keyValues["stop"] is not { IsNumber: true })
        {
            throw Error(keyValues["stop"] ?? keyValues, "The sequence of 'keyvals' needs a numeric 'stop'.");
        }

        if (keyValues["start"] is { } start && !start.IsNumber)
        {
            throw Error(start, "The 'start' of the sequence of 'keyvals' must be a number.");
        }

        if (keyValues["step"] is { } step && (!step.IsNumber || step.NumberValue == 0))
        {
            throw Error(step, "The 'step' of the sequence of 'keyvals' must be a number other than 0.");
        }
    }

    /// <summary>
    ///     Validates an array of sort fields <c>{field, order}</c> (the <c>sort</c> of a window or a stack).
    /// </summary>
    /// <param name="sort">The array.</param>
    /// <param name="message">The description for a value that is not such an array.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for the first entry that is not valid.</exception>
    private void ValidateSortFields(SpecNode sort, string message)
    {
        if (!sort.IsArray)
        {
            throw Error(sort, message);
        }

        foreach (var item in sort.Items)
        {
            if (!item.IsObject)
            {
                throw Error(item, message);
            }

            if (item["field"] is not { } field)
            {
                throw Error(item, "A sort field needs a 'field'.");
            }

            RequireFieldPath(field, "The 'field' of a sort field must be a valid field name.");
            if (item["order"] is { IsNull: false } order && (!order.IsString || !SortOrders.Contains(order.StringValue!)))
            {
                throw Error(order, "The 'order' of a sort field must be 'ascending' or 'descending'.");
            }
        }
    }

    /// <summary>
    ///     Validates a window or impute <c>frame</c>: two values, each null or an integer.
    /// </summary>
    /// <param name="frame">The value.</param>
    /// <param name="message">The description for a value that is not such a pair.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not a pair of integers or nulls.</exception>
    private void ValidateFrame(SpecNode frame, string message)
    {
        if (!frame.IsArray || frame.Items.Count != 2)
        {
            throw Error(frame, message);
        }

        foreach (var bound in frame.Items)
        {
            if (!bound.IsNull && !IsInteger(bound, int.MinValue, int.MaxValue))
            {
                throw Error(bound, message);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // Encoding properties
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Validates the <c>aggregate</c>, <c>bin</c>, <c>stack</c> and <c>sort</c> of a channel definition.
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when one of the properties has a wrong value.</exception>
    private void ValidateAggregationProperties(SpecNode definition)
    {
        if (definition["aggregate"] is { IsNull: false } aggregate)
        {
            ValidateEncodingAggregate(aggregate);
        }

        if (definition["bin"] is { IsObject: true } bin)
        {
            ValidateBinParameters(bin);
        }
        else if (definition["bin"] is { IsString: true } binText && binText.StringValue != "binned")
        {
            throw Error(binText, "The 'bin' of a channel must be true, false, null, 'binned' or an object of bin parameters.");
        }

        if (definition["stack"] is { IsString: true } stack && !StackOffsetNames.Contains(stack.StringValue!))
        {
            throw Error(stack, "The 'stack' of a channel must be 'zero', 'center', 'normalize', true, false or null.");
        }

        if (definition["sort"] is { } sort)
        {
            ValidateEncodingSort(definition, sort);
        }
    }

    /// <summary>
    ///     Validates the <c>aggregate</c> of a channel definition: an operation name, or an object for <c>argmin</c>, <c>argmax</c> and the exponential operations.
    /// </summary>
    /// <param name="aggregate">The value (not null).</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not valid.</exception>
    private void ValidateEncodingAggregate(SpecNode aggregate)
    {
        if (aggregate.IsString)
        {
            if (!AggregateMeasures.TryParse(aggregate.StringValue, out var op))
            {
                throw Error(aggregate, $"'{aggregate.StringValue}' is not an aggregate operation.");
            }

            if (AggregateMeasures.IsArgOp(op))
            {
                throw Error(aggregate, $"The '{aggregate.StringValue}' aggregate takes the field to compare: write {{\"{aggregate.StringValue}\": \"field\"}}.");
            }

            return;
        }

        if (aggregate.Properties.Count != 1)
        {
            throw Error(aggregate, "The aggregate object must have exactly one property: argmax, argmin, exponential or exponentialb.");
        }

        var property = aggregate.Properties[0];
        switch (property.Name)
        {
            case "argmax" or "argmin":
                RequireFieldPath(property, $"The '{property.Name}' of an aggregate must be a valid field name.");
                break;
            case "exponential" or "exponentialb":
                if (!property.IsNumber || property.NumberValue <= 0 || property.NumberValue > 1)
                {
                    throw Error(property, $"The '{property.Name}' of an aggregate must be a number greater than 0 and at most 1.");
                }

                break;
            default:
                throw Error(aggregate, "The aggregate object must have one property: argmax, argmin, exponential or exponentialb.");
        }
    }

    /// <summary>
    ///     Validates the <c>sort</c> of a channel definition.
    /// </summary>
    /// <remarks>
    ///     The accepted forms are <c>ascending</c>, <c>descending</c>, null, an array of values, a channel shorthand
    ///     (<c>"y"</c>, <c>"-y"</c>) and an object <c>{field, op, order}</c> or <c>{encoding, order}</c>. The <c>order</c> channel
    ///     takes only <c>ascending</c> and <c>descending</c>. The items of an array are checked by <see cref="ValidateSortArray"/>
    ///     and the keys of an object that Vega-Lite does not define are warned about (<c>vl-unknown-property</c>).
    /// </remarks>
    /// <param name="definition">The channel definition that owns the sort.</param>
    /// <param name="sort">The <c>sort</c> value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not one of the forms.</exception>
    private void ValidateEncodingSort(SpecNode definition, SpecNode sort)
    {
        var channel = definition.Name ?? definition.Parent?.Name;
        if (channel == "order")
        {
            if (!sort.IsString || !SortOrders.Contains(sort.StringValue!))
            {
                throw Error(sort, "The 'sort' of an order channel must be 'ascending' or 'descending'.");
            }

            return;
        }

        switch (sort.Kind)
        {
            case SpecNodeKind.String:
                if (!SortOrders.Contains(sort.StringValue!) && !TryParseSortShorthand(sort.StringValue, out _, out _))
                {
                    throw Error(sort, "The 'sort' must be 'ascending', 'descending', a channel such as 'y' or '-y', an array of values, an object or null.");
                }

                break;
            case SpecNodeKind.Object:
                ValidateSortObject(sort);
                break;
            case SpecNodeKind.Array:
                ValidateSortArray(definition, sort);
                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Validates a <c>sort</c> array: the values in the order the categories take.
    /// </summary>
    /// <remarks>
    ///     Every item is a string, a number, a boolean or null. An object is accepted only as a date-time (an object of date-time
    ///     keys such as <c>{"year": 2024, "month": 3}</c>) on a field of type <c>temporal</c>; any other object or array item would
    ///     otherwise be read as the null category and silently mis-sort, so it is an error at the item. An array that mixes
    ///     kinds of values is accepted (the values are matched by kind, as Vega-Lite's strict comparison does) and reported
    ///     as the warning <c>vl-encoding-sort-mixed-types</c>.
    /// </remarks>
    /// <param name="definition">The channel definition that owns the sort.</param>
    /// <param name="sort">The array.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the item, when an item is an array or an object that is not a date-time of a temporal field.</exception>
    private void ValidateSortArray(SpecNode definition, SpecNode sort)
    {
        var temporal = definition["type"] is { IsString: true, StringValue: "temporal" };
        var kinds = new HashSet<SpecNodeKind>();
        foreach (var item in sort.Items)
        {
            if (item.IsObject && temporal && IsDateTimeObject(item))
            {
                continue;
            }

            if (item.IsObject || item.IsArray)
            {
                throw Error(item, temporal
                    ? "An item of a 'sort' array must be a string, a number, a boolean, null or a date-time object such as {\"year\": 2024}."
                    : "An item of a 'sort' array must be a string, a number, a boolean or null (a date-time object needs a field of type 'temporal').");
            }

            kinds.Add(item.Kind);
        }

        if (kinds.Count > 1)
        {
            _warnings.Add(new FeatureReport(
                "vl-encoding-sort-mixed-types",
                "The 'sort' array mixes strings, numbers, booleans or null; values are matched by kind, so a value of another kind than the data's does not position it.",
                sort.GetLocation(_file),
                FeatureReportKind.Warning));
        }
    }

    /// <summary>
    ///     Determines whether an object is a Vega-Lite date-time (a non-empty object with only date-time keys).
    /// </summary>
    /// <param name="node">The object.</param>
    /// <returns><see langword="true"/> for an object whose every key is a date-time key with a string, number or boolean value.</returns>
    private static bool IsDateTimeObject(SpecNode node) =>
        node.Properties.Count > 0
        && node.Properties.All(p => DateTimeKeys.Contains(p.Name!) && (p.IsString || p.IsNumber || p.IsBoolean));

    /// <summary>
    ///     Validates a <c>sort</c> object: <c>{field, op, order}</c> (sort by an operation over a field) or <c>{encoding, order}</c> (sort by another channel).
    /// </summary>
    /// <param name="sort">The object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when a property is not valid or the two forms are mixed.</exception>
    private void ValidateSortObject(SpecNode sort)
    {
        foreach (var property in sort.Properties.Where(p => !SortObjectKeys.Contains(p.Name!)))
        {
            WarnUnknown(property, "a sort definition");
        }

        if (sort["order"] is { IsNull: false } order && (!order.IsString || !SortOrders.Contains(order.StringValue!)))
        {
            throw Error(order, "The 'order' of a sort must be 'ascending' or 'descending'.");
        }

        if (sort["encoding"] is { } encoding)
        {
            if (!encoding.IsString || !SortChannels.Contains(encoding.StringValue!))
            {
                throw Error(encoding, "The 'encoding' of a sort must be one of the channels x, y, color, fill, stroke, strokeWidth, size, shape, fillOpacity, strokeOpacity, opacity or text.");
            }

            var mixed = sort["field"] ?? sort["op"];
            if (mixed is not null)
            {
                throw Error(mixed, "A sort by 'encoding' cannot also have a 'field' or an 'op'.");
            }

            return;
        }

        if (sort["field"] is { } field)
        {
            RequireFieldPath(field, "The 'field' of a sort must be a valid field name.");
        }

        if (sort["op"] is { } op
            && (!op.IsString || !AggregateMeasures.TryParse(op.StringValue, out var parsed) || AggregateMeasures.IsArgOp(parsed)))
        {
            throw Error(op, "The 'op' of a sort must be an aggregate operation other than argmin and argmax.");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // Small checks
    // ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Determines whether a value is a whole number in a range.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <returns><see langword="true"/> for a number with no fraction in the range.</returns>
    private static bool IsInteger(SpecNode node, long min, long max) =>
        node.IsNumber && double.IsInteger(node.NumberValue) && node.NumberValue >= min && node.NumberValue <= max;

    /// <summary>
    ///     Checks that a value is a number in a range.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <param name="message">The description of the problem.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not a number in the range.</exception>
    private void RequireNumber(SpecNode node, double min, double max, string message)
    {
        if (!node.IsNumber || node.NumberValue < min || node.NumberValue > max)
        {
            throw Error(node, message);
        }
    }

    /// <summary>
    ///     Checks that a value is a non-empty array of positive numbers of limited length.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="maxCount">The most numbers.</param>
    /// <param name="message">The description of the problem.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the array or the offending number, when the value is not valid.</exception>
    private void RequireNumberList(SpecNode node, int maxCount, string message)
    {
        if (!node.IsArray || node.Items.Count == 0 || node.Items.Count > maxCount)
        {
            throw Error(node, message);
        }

        foreach (var item in node.Items)
        {
            if (!item.IsNumber || item.NumberValue <= 0)
            {
                throw Error(item, message);
            }
        }
    }
}
