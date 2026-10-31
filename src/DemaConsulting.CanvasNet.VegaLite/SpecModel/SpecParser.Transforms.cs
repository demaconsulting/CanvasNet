using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     Validation of expressions, variable parameters and the row transforms that the renderer executes.
/// </content>
/// <remarks>
///     <para>
///         The rule is the same as for the rest of the specification: a construct whose shape cannot be what its author
///         meant (a filter that is neither an expression nor a predicate, a field name that is not a path, an expression
///         with a syntax error, a parameter cycle, a reference to a parameter that does not exist) is a
///         <see cref="VegaLiteSpecException"/> at the exact value, with the position of the value in the text and, for an
///         expression, the offset inside the expression text in the message. Constructs that are well formed but that the
///         renderer does not execute (a function outside the whitelist, view state, a selection parameter, a transform of a
///         later phase) are not errors: the feature catalog reports them.
///     </para>
///     <para>
///         The expressions of the supported sites are compiled here, once, and kept in the <see cref="SpecTree"/>: the
///         <c>expr</c> of every variable parameter, the <c>calculate</c> and the string <c>filter</c> of every transform
///         (also those inside a predicate composition) and the <c>{"expr": ...}</c> values of the paint properties of the mark
///         and of the <c>value</c> of the paint channels of the drawn view. Predicate nesting is bounded by
///         <see cref="VegaLiteLimits.MaxJsonDepth"/>, so the recursive predicate check cannot exhaust the stack.
///     </para>
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The names of the signals of the view that an expression may read; they are view state, not parameters.</summary>
    private static readonly HashSet<string> ViewSignals = Set("width height padding autosize background cursor");

    /// <summary>The parameter names that Vega-Lite reserves.</summary>
    private static readonly HashSet<string> ReservedParameterNames = Set("datum event item parent");

    /// <summary>The composition properties; a view that has one is not drawn.</summary>
    private static readonly string[] CompositionKeys = ["layer", "concat", "hconcat", "vconcat", "facet", "repeat", "spec"];

    /// <summary>The mark properties whose <c>{"expr": ...}</c> value is evaluated for every row.</summary>
    private static readonly string[] ExpressionMarkProperties = ["color", "fill", "stroke", "opacity", "strokeWidth", "size"];

    /// <summary>The channels whose constant <c>value</c> may be an expression evaluated for every row.</summary>
    private static readonly HashSet<string> ExpressionChannels = Set("color fill stroke opacity size");

    /// <summary>The marks that draw symbols, the only ones whose <c>size</c> is the area of the symbol and may be an expression.</summary>
    private static readonly HashSet<string> SymbolMarks = Set("point circle square");

    /// <summary>The predicate operators of a field predicate, in the order they are looked for.</summary>
    private static readonly string[] PredicateOperators = ["equal", "lt", "lte", "gt", "gte", "range", "oneOf", "valid"];

    /// <summary>The parameter name indexes of the <c>params</c> arrays, built on first use (the trees are immutable, so an index never goes stale).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SpecNode, Dictionary<string, SpecNode>> ParameterIndexes = [];

    /// <summary>The root object of the specification being validated.</summary>
    private SpecNode? _root;

    /// <summary>
    ///     Determines whether a name is a signal of the view (<c>width</c>, <c>height</c>, ...), which an expression may
    ///     mention but which is view state that the renderer does not provide to expressions.
    /// </summary>
    /// <param name="name">The identifier.</param>
    /// <returns><see langword="true"/> for a signal name.</returns>
    public static bool IsViewSignal(string name) => ViewSignals.Contains(name);

    /// <summary>
    ///     Determines whether a name is one of the transforms whose result the renderer computes or accepts.
    /// </summary>
    /// <param name="name">The transform name.</param>
    /// <returns><see langword="true"/> for <c>calculate</c>, <c>filter</c>, <c>fold</c>, <c>flatten</c>, <c>sample</c>, <c>pivot</c>, <c>lookup</c>, <c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>, <c>stack</c>, <c>impute</c> and <c>timeUnit</c>.</returns>
    public static bool IsSupportedTransform(string name) =>
        name is "calculate" or "filter" or "fold" or "flatten" or "sample" or "pivot" or "lookup"
            or "aggregate" or "joinaggregate" or "window" or "bin" or "stack" or "impute" or "timeUnit";

    /// <summary>
    ///     Determines whether a view is the one the renderer draws: the root of a specification that is not a composition.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <param name="view">The view.</param>
    /// <returns><see langword="true"/> for the root without a composition property.</returns>
    public static bool IsDrawnView(SpecNode root, SpecNode view) =>
        ReferenceEquals(root, view) && !CompositionKeys.Any(key => view[key] is not null);

    /// <summary>
    ///     Determines whether a parameter of that name is declared in the view that contains a node or in an ancestor view.
    /// </summary>
    /// <param name="from">The node that refers to the parameter.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns><see langword="true"/> when a parameter has that name.</returns>
    public static bool IsParameterDeclared(SpecNode from, string name) => FindParameter(from, name) is not null;

    /// <summary>
    ///     Finds the declaration of a parameter visible from a node: the nearest view (the node's own, then its ancestors) that declares it.
    /// </summary>
    /// <param name="from">The node that refers to the parameter.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter object, or null when none is declared.</returns>
    public static SpecNode? FindParameter(SpecNode from, string name)
    {
        for (SpecNode? node = from; node is not null; node = node.Parent)
        {
            if (node.IsObject && node["params"] is { IsArray: true } list
                && ParameterIndexes.GetValue(list, BuildParameterIndex).TryGetValue(name, out var found))
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    ///     Indexes the parameters of a <c>params</c> array by name, so that the lookup of a reference does not depend on the number of parameters.
    /// </summary>
    /// <param name="list">The <c>params</c> array.</param>
    /// <returns>The parameter objects by name; the first declaration of a name wins.</returns>
    private static Dictionary<string, SpecNode> BuildParameterIndex(SpecNode list)
    {
        var index = new Dictionary<string, SpecNode>(StringComparer.Ordinal);
        foreach (var parameter in list.Items)
        {
            if (parameter.IsObject && parameter["name"]?.StringValue is { } name)
            {
                index.TryAdd(name, parameter);
            }
        }

        return index;
    }

    /// <summary>
    ///     Compiles the expression text of a string node and checks the parameters it refers to.
    /// </summary>
    /// <param name="text">The string node that holds the expression.</param>
    /// <returns>The compiled expression (also kept in the tree).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown for a syntax error, a limit overflow or a reference to an unknown parameter.</exception>
    private CompiledExpression CompileExpression(SpecNode text) => CompileSource(text, text.StringValue!, text);

    /// <summary>
    ///     Compiles expression source for a node of the specification and checks the parameters it refers to.
    /// </summary>
    /// <param name="key">The node the compiled expression is kept under (the string node, or the predicate object that the source was written from).</param>
    /// <param name="source">The expression source.</param>
    /// <param name="location">The node that locates a failure, and whose scope decides which parameters are visible.</param>
    /// <returns>The compiled expression (also kept in the tree).</returns>
    /// <exception cref="VegaLiteSpecException">Thrown for a syntax error, a limit overflow or a reference to an unknown parameter.</exception>
    private CompiledExpression CompileSource(SpecNode key, string source, SpecNode location)
    {
        var compiled = ExpressionParser.Parse(source, location.GetLocation(_file), _limits);
        var unknown = compiled.ParameterReferences.FirstOrDefault(r => !IsViewSignal(r.Name) && !IsParameterDeclared(location, r.Name));
        if (unknown.Name is not null)
        {
            throw Error(
                location,
                $"The expression refers to '{unknown.Name}', which is not a parameter of the specification (at expression offset {unknown.Offset}).");
        }

        _expressions[key] = compiled;
        return compiled;
    }

    /// <summary>
    ///     Validates the <c>condition</c> of a channel definition: each condition is an object with a <c>test</c> (an expression or a
    ///     predicate) or a <c>param</c>, and a <c>value</c> or the properties of a field definition.
    /// </summary>
    /// <remarks>
    ///     The test of a condition on a drawn channel is compiled here, once: an expression text as it is, a predicate object as the
    ///     expression that means the same (<see cref="PredicateSource"/>). A condition on a parameter is not evaluated (the feature catalog reports it),
    ///     so it is validated for its shape only.
    /// </remarks>
    /// <param name="definition">The channel definition object, whose property kinds have been checked.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending value, for a condition that is not an object, has neither a test nor a parameter, or has an invalid test.</exception>
    private void ValidateConditions(SpecNode definition)
    {
        if (definition["condition"] is not { } condition)
        {
            return;
        }

        var items = condition.IsArray ? condition.Items : [condition];
        var evaluated = IsConditionSite(definition);
        foreach (var item in items)
        {
            if (!item.IsObject)
            {
                throw Error(item, "Each condition must be an object with a 'test' or a 'param'.");
            }

            CheckObject(item, ConditionRules);
            ValidateAggregationProperties(item);
            foreach (var property in item.Properties.Where(p => !ChannelDefKeys.Contains(p.Name!)))
            {
                WarnUnknown(property, "a condition");
            }

            if (item["timeUnit"] is { } timeUnit)
            {
                ValidateTimeUnit(timeUnit);
            }

            ValidateDatum(item);
            if (item["test"] is null && item["param"] is null)
            {
                throw Error(item, "A condition needs a 'test' (an expression or a predicate) or a 'param'.");
            }

            ValidateConditionGuides(item);
            if (item["test"] is { } test)
            {
                ValidatePredicate(test);
                if (evaluated && test.IsObject && PredicateSource.TryBuild(test, out var source))
                {
                    CompileSource(test, source, test);
                }
            }

            if (evaluated && item["value"] is { IsObject: true } value && value["expr"] is { IsString: true } text)
            {
                CompileExpression(text);
            }
        }
    }

    /// <summary>
    ///     Validates the scale, axis and legend of the field definition that a condition holds.
    /// </summary>
    /// <param name="condition">The condition object.</param>
    private void ValidateConditionGuides(SpecNode condition)
    {
        if (condition["scale"] is { IsObject: true } scale)
        {
            CheckObject(scale, ScaleRules);
            ValidateScaleOptions(scale);
        }

        if (condition["axis"] is { IsObject: true } axis)
        {
            CheckObject(axis, AxisRules);
            ValidateGuideTime(axis);
        }

        if (condition["legend"] is { IsObject: true } legend)
        {
            CheckObject(legend, LegendRules);
            ValidateLegendOptions(legend);
            ValidateGuideTime(legend);
        }
    }

    /// <summary>
    ///     Tells whether the conditions of a channel definition are evaluated: the channel is one that accepts a condition and belongs to the drawn view
    ///     (and a size only on a mark that uses it: the symbol marks, bar, rect, tick, rule, and the line, trail and text marks, whose size is the stroke width,
    ///     the width of the trail and the font size).
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <returns><see langword="true"/> when the marks evaluate the conditions.</returns>
    private bool IsConditionSite(SpecNode definition)
    {
        if (_root is null
            || definition.Name is not { } channel
            || !ChannelDefinitions.CanCondition(channel)
            || definition.Parent?.Parent is not { } view
            || !IsDrawnView(_root, view))
        {
            return false;
        }

        var markNode = view["mark"];
        var markType = markNode is { IsObject: true } ? markNode["type"]?.AsString() : markNode?.AsString();
        return channel != "size" || (markType is not null && (SymbolMarks.Contains(markType) || markType is "bar" or "rect" or "tick" or "rule" or "line" or "trail" or "text"));
    }

    /// <summary>
    ///     Validates the semantics of the entries of <c>params</c>: unique names, variable parameter expressions and the
    ///     absence of cycles.
    /// </summary>
    /// <param name="parameters">The <c>params</c> array, already checked for its basic shape.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a repeated or reserved name, an expression error or a cycle.</exception>
    private void ValidateParameterSemantics(SpecNode parameters)
    {
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var compiled = new List<(int Index, CompiledExpression Expression)>();
        for (var i = 0; i < parameters.Items.Count; i++)
        {
            var parameter = parameters.Items[i];
            var name = parameter["name"]!;
            if (ReservedParameterNames.Contains(name.StringValue!))
            {
                throw Error(name, $"'{name.StringValue}' is reserved and cannot be the name of a parameter.");
            }

            if (!names.TryAdd(name.StringValue!, i))
            {
                throw Error(name, $"The parameter '{name.StringValue}' is declared more than once in this view.");
            }

            // A selection parameter has no value to compute; only variable parameters carry expressions
            if (parameter["select"] is null && parameter["expr"] is { IsString: true } expression)
            {
                compiled.Add((i, CompileExpression(expression)));
            }
        }

        // The order of evaluation is a topological order of the references between the parameters of this view
        var dependencies = new int[parameters.Items.Count][];
        for (var i = 0; i < dependencies.Length; i++)
        {
            dependencies[i] = [];
        }

        foreach (var (index, expression) in compiled)
        {
            dependencies[index] = expression.ParameterReferences
                .Where(r => names.ContainsKey(r.Name) && parameters.Items[names[r.Name]]["select"] is null)
                .Select(r => names[r.Name])
                .ToArray();
        }

        var (_, unresolved) = ParameterGraph.Sort(dependencies);
        if (unresolved >= 0)
        {
            var offender = parameters.Items[unresolved];
            throw Error(
                offender["expr"] ?? offender["name"]!,
                $"The parameter '{offender["name"]!.StringValue}' is part of, or depends on, a cycle of parameters that refer to each other.");
        }
    }

    /// <summary>
    ///     Validates one entry of <c>transform</c> that names a transform and compiles its expressions.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The property that names the transform.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateTransformEntry(SpecNode entry, SpecNode operation)
    {
        switch (operation.Name)
        {
            case "calculate":
                RequireOutputName(entry, "as", "The calculate transform needs a text 'as' that names the new field.");
                CompileExpression(operation);
                break;
            case "filter":
                ValidateFilter(operation);
                break;
            case "fold":
                ValidateFieldList(operation, "The fold must be an array of field names.");
                ValidateOutputNames(entry["as"], 1, 2, "The 'as' of a fold must be an array of one or two field names.");
                break;
            case "flatten":
                ValidateFieldList(operation, "The flatten must be an array of field names.");
                ValidateOutputNames(entry["as"], 1, operation.Items.Count, "The 'as' of a flatten must be an array with at most as many field names as the flatten has fields.");
                break;
            case "sample":
                if (!operation.IsNumber || operation.NumberValue < 0)
                {
                    throw Error(operation, "The sample must be a number that is not negative.");
                }

                break;
            case "pivot":
                ValidatePivot(entry, operation);
                break;
            case "lookup":
                ValidateLookup(entry, operation);
                break;
            case "aggregate" or "joinaggregate":
                ValidateAggregateTransform(entry, operation);
                break;
            case "window":
                ValidateWindowTransform(entry, operation);
                break;
            case "bin":
                ValidateBinTransform(entry, operation);
                break;
            case "stack":
                ValidateStackTransform(entry, operation);
                break;
            case "impute":
                ValidateImputeTransform(entry, operation);
                break;
            case "timeUnit":
                ValidateTimeUnitTransform(entry, operation);
                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Checks that a value is a text that is a valid field path.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="message">The description for a value that is not a field path.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not a valid field path.</exception>
    private void RequireFieldPath(SpecNode node, string message)
    {
        if (!node.IsString || FieldPath.Canonicalize(node.StringValue) is null)
        {
            throw Error(node, message);
        }
    }

    /// <summary>
    ///     Checks that an array is made of valid field paths.
    /// </summary>
    /// <param name="list">The value.</param>
    /// <param name="message">The description for a value that is not an array of field paths.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not an array of valid field paths.</exception>
    private void ValidateFieldList(SpecNode list, string message)
    {
        if (!list.IsArray)
        {
            throw Error(list, message);
        }

        foreach (var field in list.Items)
        {
            RequireFieldPath(field, "A field name must be a text that is a valid field path.");
        }
    }

    /// <summary>
    ///     Checks that a property of an entry is a non-empty text.
    /// </summary>
    /// <param name="entry">The object.</param>
    /// <param name="name">The property.</param>
    /// <param name="message">The description of the problem.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the property is missing, not a text or empty.</exception>
    private void RequireOutputName(SpecNode entry, string name, string message)
    {
        var node = entry[name];
        if (node is null || !node.IsString || node.StringValue!.Length == 0)
        {
            throw Error(node ?? entry, message);
        }
    }

    /// <summary>
    ///     Checks an optional array of output field names.
    /// </summary>
    /// <param name="node">The value, or null.</param>
    /// <param name="min">The fewest names.</param>
    /// <param name="max">The most names.</param>
    /// <param name="message">The description of the problem.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is present and not an array of that many non-empty texts.</exception>
    private void ValidateOutputNames(SpecNode? node, int min, int max, string message)
    {
        if (node is null)
        {
            return;
        }

        if (!node.IsArray || node.Items.Count < min || node.Items.Count > max || node.Items.Any(n => !n.IsString || n.StringValue!.Length == 0))
        {
            throw Error(node, message);
        }
    }

    /// <summary>
    ///     Validates the value of a <c>filter</c>: an expression text or a predicate.
    /// </summary>
    /// <param name="filter">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a value that is neither, or an invalid expression or predicate.</exception>
    private void ValidateFilter(SpecNode filter)
    {
        if (!filter.IsString && !filter.IsObject)
        {
            throw Error(filter, "The filter must be an expression text or a predicate object.");
        }

        ValidatePredicate(filter);
    }

    /// <summary>
    ///     Validates a predicate: an expression text, a logical composition, a parameter predicate or a field predicate.
    /// </summary>
    /// <param name="predicate">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not a valid predicate.</exception>
    private void ValidatePredicate(SpecNode predicate)
    {
        if (predicate.IsString)
        {
            CompileExpression(predicate);
            return;
        }

        if (!predicate.IsObject)
        {
            throw Error(predicate, "A predicate must be an expression text or an object.");
        }

        foreach (var composition in new[] { "and", "or" })
        {
            if (predicate[composition] is { } items)
            {
                if (!items.IsArray)
                {
                    throw Error(items, $"The '{composition}' of a predicate must be an array of predicates.");
                }

                foreach (var item in items.Items)
                {
                    ValidatePredicate(item);
                }

                return;
            }
        }

        if (predicate["not"] is { } negated)
        {
            ValidatePredicate(negated);
            return;
        }

        if (predicate["param"] is { } parameter)
        {
            if (!parameter.IsString || !IsParameterDeclared(parameter, parameter.StringValue!))
            {
                throw Error(parameter, "The 'param' of a predicate must be the name of a parameter of the specification.");
            }

            return;
        }

        if (predicate["selection"] is { } selection)
        {
            if (!selection.IsString)
            {
                throw Error(selection, "The 'selection' of a predicate must be a text.");
            }

            return;
        }

        ValidateFieldPredicate(predicate);
    }

    /// <summary>
    ///     Validates a field predicate: a field and one comparison.
    /// </summary>
    /// <param name="predicate">The predicate object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the object is not a valid field predicate.</exception>
    private void ValidateFieldPredicate(SpecNode predicate)
    {
        var field = predicate["field"];
        if (field is null)
        {
            throw Error(predicate, "The object is not a Vega-Lite predicate: it needs a 'field' (or 'and', 'or', 'not' or 'param').");
        }

        RequireFieldPath(field, "The 'field' of a predicate must be a valid field name.");
        var comparison = PredicateOperators.Select(name => predicate[name]).FirstOrDefault(node => node is not null);
        if (comparison is null)
        {
            throw Error(predicate, "A field predicate needs one of equal, lt, lte, gt, gte, range, oneOf or valid.");
        }

        switch (comparison.Name)
        {
            case "range":
                if (!comparison.IsArray || comparison.Items.Count != 2 || comparison.Items.Any(IsStructured))
                {
                    throw Error(comparison, "The 'range' of a predicate must be an array of two values.");
                }

                break;
            case "oneOf":
                if (!comparison.IsArray || comparison.Items.Any(IsStructured))
                {
                    throw Error(comparison, "The 'oneOf' of a predicate must be an array of values.");
                }

                break;
            case "valid":
                if (!comparison.IsBoolean)
                {
                    throw Error(comparison, "The 'valid' of a predicate must be true or false.");
                }

                break;
            default:
                if (comparison.IsArray)
                {
                    throw Error(comparison, $"The '{comparison.Name}' of a predicate must be a single value.");
                }

                break;
        }

        ValidatePredicateTime(predicate, comparison);
        CompileTimePredicate(predicate);
    }

    /// <summary>
    ///     Determines whether a value is an array (a value that a predicate cannot compare with).
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns><see langword="true"/> for an array.</returns>
    private static bool IsStructured(SpecNode node) => node.IsArray;

    /// <summary>
    ///     Validates a <c>pivot</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The pivot field.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidatePivot(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The pivot must be the name of the field to pivot.");
        if (entry["value"] is not { } value)
        {
            throw Error(entry, "The pivot transform needs a 'value' field.");
        }

        RequireFieldPath(value, "The 'value' of a pivot must be a valid field name.");
        if (entry["groupby"] is { } groupBy)
        {
            ValidateFieldList(groupBy, "The 'groupby' of a pivot must be an array of field names.");
        }

        if (entry["limit"] is { } limit && (!limit.IsNumber || limit.NumberValue < 0))
        {
            throw Error(limit, "The 'limit' of a pivot must be a number that is not negative.");
        }

        if (entry["op"] is { } op && !op.IsString)
        {
            throw Error(op, "The 'op' of a pivot must be a text.");
        }
    }

    /// <summary>
    ///     Validates a <c>lookup</c> transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field of the main data that is looked up.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the entry has the wrong shape.</exception>
    private void ValidateLookup(SpecNode entry, SpecNode operation)
    {
        RequireFieldPath(operation, "The lookup must be the name of a field of the data.");
        if (entry["from"] is not { IsObject: true } from)
        {
            throw Error(entry["from"] ?? entry, "The lookup transform needs a 'from' object.");
        }

        if (from["param"] is { } selection)
        {
            // A lookup in the data of a selection is reported as not supported; its shape is not read
            if (!selection.IsString)
            {
                throw Error(selection, "The 'param' of a lookup source must be a text.");
            }

            return;
        }

        if (from["data"] is not { IsObject: true } data)
        {
            throw Error(from["data"] ?? from, "The 'from' of a lookup needs a 'data' object.");
        }

        ValidateData(data);
        if (from["key"] is not { } key)
        {
            throw Error(from, "The 'from' of a lookup needs the 'key' field of the other data.");
        }

        RequireFieldPath(key, "The 'key' of a lookup source must be a valid field name.");
        var fields = from["fields"];
        if (fields is not null)
        {
            ValidateFieldList(fields, "The 'fields' of a lookup source must be an array of field names.");
        }

        ValidateLookupOutput(entry, fields);
    }

    /// <summary>
    ///     Validates the <c>as</c> and <c>default</c> of a lookup against its <c>fields</c>.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="fields">The <c>from.fields</c> array, or null when the whole row is copied.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the output names do not fit the fields.</exception>
    private void ValidateLookupOutput(SpecNode entry, SpecNode? fields)
    {
        if (entry["as"] is { } output)
        {
            var count = fields?.Items.Count ?? 1;
            if (output.IsString)
            {
                if (output.StringValue!.Length == 0 || count != 1)
                {
                    throw Error(output, "The 'as' of a lookup must name one non-empty field for each looked-up field.");
                }
            }
            else
            {
                ValidateOutputNames(output, count, count, "The 'as' of a lookup must name one non-empty field for each looked-up field.");
            }
        }

        if (entry["default"] is { } fallback && (fallback.IsObject || fallback.IsArray))
        {
            throw Error(fallback, "The 'default' of a lookup must be a text, a number, a boolean or null.");
        }
    }

    /// <summary>
    ///     Compiles the <c>{"expr": ...}</c> values of the paint properties of the mark of the drawn view.
    /// </summary>
    /// <param name="mark">The mark definition object.</param>
    /// <param name="markType">The mark type.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for an expression error.</exception>
    private void CompileMarkExpressions(SpecNode mark, string markType)
    {
        if (_root is null || mark.Parent is not { } view || !IsDrawnView(_root, view))
        {
            return;
        }

        foreach (var name in ExpressionMarkProperties)
        {
            if (mark[name] is { IsObject: true } reference && reference["expr"] is { IsString: true } text && (name != "size" || SymbolMarks.Contains(markType)))
            {
                CompileExpression(text);
            }
        }
    }

    /// <summary>
    ///     Compiles the <c>{"expr": ...}</c> value of a paint channel of the drawn view.
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for an expression error.</exception>
    private void CompileChannelExpression(SpecNode definition)
    {
        if (_root is null
            || definition.Name is not { } channel
            || !ExpressionChannels.Contains(channel)
            || definition.Parent?.Parent is not { } view
            || !IsDrawnView(_root, view)
            || definition["value"] is not { IsObject: true } reference
            || reference["expr"] is not { IsString: true } text)
        {
            return;
        }

        var markNode = view["mark"];
        var markType = markNode is { IsObject: true } ? markNode["type"]?.AsString() : markNode?.AsString();
        if (channel == "size" && (markType is null || !SymbolMarks.Contains(markType)))
        {
            return;
        }

        CompileExpression(text);
    }
}

/// <summary>
///     Orders parameters so that each one comes after the parameters its expression refers to.
/// </summary>
/// <remarks>
///     The order is the topological order that takes the earliest declared ready parameter first, so it is deterministic
///     and independent of hashing. Parameters that are part of a cycle, or that depend on one, are never ready.
/// </remarks>
internal static class ParameterGraph
{
    /// <summary>
    ///     Sorts parameters by their dependencies.
    /// </summary>
    /// <param name="dependencies">For each parameter (by declaration index) the indexes of the parameters it depends on.</param>
    /// <returns>The evaluation order, and the lowest declaration index of a parameter that could not be ordered (-1 when there is no cycle).</returns>
    public static (int[] Order, int FirstUnresolved) Sort(IReadOnlyList<int[]> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        var count = dependencies.Count;
        var remaining = new int[count];
        var dependents = new List<int>[count];
        for (var i = 0; i < count; i++)
        {
            dependents[i] = [];
        }

        for (var i = 0; i < count; i++)
        {
            // A repeated reference counts once, so the count falls to zero when each distinct parameter is done
            foreach (var dependency in dependencies[i].Distinct())
            {
                remaining[i]++;
                dependents[dependency].Add(i);
            }
        }

        var ready = new SortedSet<int>(Enumerable.Range(0, count).Where(i => remaining[i] == 0));
        var order = new List<int>(count);
        while (ready.Count > 0)
        {
            var next = ready.Min;
            ready.Remove(next);
            order.Add(next);
            foreach (var dependent in dependents[next])
            {
                if (--remaining[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        var unresolved = -1;
        for (var i = 0; i < count && unresolved < 0; i++)
        {
            if (remaining[i] > 0)
            {
                unresolved = i;
            }
        }

        return (order.ToArray(), unresolved);
    }
}
